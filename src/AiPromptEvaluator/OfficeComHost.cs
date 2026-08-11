using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AiPromptEvaluator;

/// <summary>
/// Runs a block of work against an Office application over COM automation, and guarantees
/// the application is gone afterwards.
///
/// Two hazards make this worth centralising. COM often hands back the user's *already
/// running* Word or Excel rather than a fresh instance, so quitting blindly would close
/// their session and lose unsaved work. And an application that fails to open a file can
/// survive <c>Quit</c> entirely, leaking a process per attempt. Both are handled here by
/// diffing the process list around instance creation and only ever touching what we started.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class OfficeComHost
{
    /// <summary>
    /// Serialises every Office automation call in the process. Two callers starting Word or
    /// Excel at the same time would each see the other's new process in their own PID diff,
    /// and could quit or kill an instance they don't own. Uploads still run concurrently —
    /// only the COM step is one-at-a-time, which is also how Office prefers to be driven.
    /// </summary>
    private static readonly SemaphoreSlim ComGate = new(1, 1);

    /// <summary>True when <paramref name="progId"/> is registered on this machine.</summary>
    public static bool IsRegistered(string progId) => Type.GetTypeFromProgID(progId) is not null;

    /// <summary>
    /// Starts the application, hands it to <paramref name="body"/>, and shuts it down.
    /// Returns null on success, or the error message on failure.
    /// </summary>
    /// <param name="progId">e.g. "Word.Application".</param>
    /// <param name="processName">The process to watch, without ".exe" — e.g. "WINWORD".</param>
    /// <param name="body">Work to perform. Must close whatever documents it opens.</param>
    public static string? Run(string progId, string processName, Action<object> body)
    {
        ComGate.Wait();
        try
        {
            return RunExclusive(progId, processName, body);
        }
        finally
        {
            ComGate.Release();
        }
    }

    private static string? RunExclusive(string progId, string processName, Action<object> body)
    {
        object? application = null;

        var preExistingPids = ProcessIds(processName);
        int[] ourPids = [];

        try
        {
            var type = Type.GetTypeFromProgID(progId);
            if (type is null)
            {
                return $"{progId} is not registered on this machine.";
            }

            application = Activator.CreateInstance(type);
            if (application is null)
            {
                return $"could not start {progId}.";
            }

            ourPids = ProcessIds(processName).Except(preExistingPids).ToArray();

            // Keep the app silent: no window, no dialogs on repair/convert prompts.
            TrySetProperty(application, "Visible", false);

            // Excel's DisplayAlerts is a bool; Word's is an enum (wdAlertsNone = 0).
            if (!TrySetProperty(application, "DisplayAlerts", false))
            {
                TrySetProperty(application, "DisplayAlerts", 0);
            }

            body(application);
            return null;
        }
        catch (COMException ex)
        {
            return ex.Message.Trim();
        }
        catch (TargetInvocationException ex)
        {
            return (ex.InnerException ?? ex).Message.Trim();
        }
        catch (Exception ex)
        {
            return ex.Message.Trim();
        }
        finally
        {
            // Only shut the app down if this call started it.
            if (ourPids.Length > 0)
            {
                Quit(application);
            }

            Release(application);

            // Drop the RCWs so the app can exit, then make sure it actually did:
            // a failed open can leave it alive with a pending internal error.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            KillSurvivors(ourPids);
        }
    }

    /// <summary>
    /// Shuts the application down. Word's Quit takes a save-changes argument and does not
    /// reliably exit without one; Excel's takes none and throws if given one. Try the
    /// argument form first, then the bare one.
    /// </summary>
    private static void Quit(object? application)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            // wdDoNotSaveChanges — discard anything the automation touched.
            Invoke(application, "Quit", 0);
        }
        catch
        {
            TryInvoke(application, "Quit");
        }
    }

    private static int[] ProcessIds(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Select(p => p.Id).ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Terminates instances this host started that failed to exit. Only ever touches process
    /// IDs that appeared while we were creating our own instance, so a session the user had
    /// open is never killed.
    /// </summary>
    private static void KillSurvivors(int[] ourPids)
    {
        foreach (var pid in ourPids)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    continue;
                }

                // Give a well-behaved instance a moment to close on its own.
                if (!process.WaitForExit(2000))
                {
                    process.Kill();
                }
            }
            catch (ArgumentException)
            {
                // Already gone — that's the outcome we wanted.
            }
            catch
            {
                // Nothing more we can do; a stray instance is better than a crash.
            }
        }
    }

    // ──────────────────────────────────────────────
    // Late-bound member access
    // ──────────────────────────────────────────────

    public static object? GetProperty(object target, string name) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);

    public static void SetProperty(object target, string name, object value) =>
        target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value]);

    public static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

    /// <summary>Sets a property the app may not expose, or may type differently. False when it didn't take.</summary>
    private static bool TrySetProperty(object target, string name, object value)
    {
        try
        {
            SetProperty(target, name, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Best-effort cleanup call — a failure here must not mask the real error.</summary>
    public static void TryInvoke(object? target, string name, params object[] args)
    {
        if (target is null)
        {
            return;
        }

        try
        {
            Invoke(target, name, args);
        }
        catch
        {
            // The app may already be gone; nothing useful to do.
        }
    }

    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            try
            {
                Marshal.ReleaseComObject(comObject);
            }
            catch
            {
                // Ignore — the object is being discarded anyway.
            }
        }
    }
}
