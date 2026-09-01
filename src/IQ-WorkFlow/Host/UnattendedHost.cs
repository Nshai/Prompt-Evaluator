using System.Globalization;
using System.IO;

using AiPromptEvaluator;

using Microsoft.Extensions.DependencyInjection;

namespace IQWorkflow;

/// <summary>
/// The entry point that makes the workflow autonomous.
///
/// <b>This is the step that turns the pipeline from a sequence of clicks into something a scheduler
/// can run.</b> The orchestration itself is already here — what was missing was a way to start it
/// that is not a person, and a way for whatever started it to learn how it went without reading a
/// log. Hence an exit code, a manifest per run, and one line per stage.
///
/// <b>Everything it depends on is injected.</b> The settings loader and the container are
/// constructor parameters with real defaults, which is what lets the whole host — argument handling,
/// batching, run-folder naming, exit codes — be driven by a test with fake stages, over no network
/// and no vector store.
/// </summary>
public sealed class UnattendedHost
{
    /// <summary>Every case completed.</summary>
    public const int Ok = 0;

    /// <summary>At least one case failed. The manifest names which stage and why.</summary>
    public const int Failed = 1;

    /// <summary>The command line or the settings could not be used. Nothing ran.</summary>
    public const int Unusable = 2;

    /// <summary>An operator stopped the run. Conventionally 128 + SIGINT.</summary>
    public const int Cancelled = 130;

    private readonly TextWriter _output;
    private readonly Func<AppSettings> _settings;
    private readonly Func<AppSettings, IServiceProvider> _services;
    private readonly Func<string, string?> _environment;

    /// <param name="environment">
    /// How an environment variable is read. Injected so a test does not depend on what the machine
    /// running it happens to have exported — the process environment is shared by every test beside
    /// it, and a credential in it would otherwise change what those tests configure.
    /// </param>
    public UnattendedHost(
        TextWriter output,
        Func<AppSettings>? settings = null,
        Func<AppSettings, IServiceProvider>? services = null,
        Func<string, string?>? environment = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _settings = settings ?? SettingsStore.Load;
        _services = services ?? DefaultServices;
        _environment = environment ?? Environment.GetEnvironmentVariable;
    }

    private static IServiceProvider DefaultServices(AppSettings settings) =>
        new ServiceCollection().AddWorkflow(settings).BuildServiceProvider();

    /// <summary>
    /// Runs the command line and returns the process's exit code.
    ///
    /// <b>A batch does not stop at the first failed case.</b> A case that fails is one case's
    /// problem — an unreadable report, a benchmark folder someone moved — and abandoning the other
    /// seven turns one bad case into a wasted night. Every failure is reported, and the exit code
    /// says that at least one happened.
    /// </summary>
    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parsed = HostArguments.Parse(args);

        if (parsed.HelpRequested)
        {
            _output.WriteLine(HostArguments.Usage);
            return Ok;
        }

        if (parsed.Error is { } error)
        {
            _output.WriteLine(error);
            _output.WriteLine();
            _output.WriteLine(HostArguments.Usage);
            return Unusable;
        }

        var settings = _settings();

        // Credentials first, and from the environment, because that is the route a scheduler has.
        // Named and never printed: the variable a key came from is worth knowing when a run fails
        // to authenticate, and the key itself is not worth putting in a log.
        var fromEnvironment = CredentialEnvironment.Apply(settings, _environment);

        if (fromEnvironment.Count > 0 && !parsed.Quiet)
        {
            _output.WriteLine($"Credentials from the environment: {string.Join(", ", fromEnvironment)}");
        }

        var rejected = SettingsOverrides.Apply(settings, parsed.Overrides);

        // Refused before anything runs, and all of them at once: an operator correcting six flags
        // one invocation at a time is six invocations, and the run in between each is not free.
        if (rejected.Count > 0)
        {
            foreach (var line in rejected)
            {
                _output.WriteLine(line);
            }

            return Unusable;
        }

        var provider = _services(settings);
        var failures = 0;

        foreach (var caseFolder in parsed.CaseFolders)
        {
            try
            {
                if (!await RunOneAsync(provider, settings, parsed, caseFolder, cancellationToken)
                        .ConfigureAwait(false))
                {
                    failures++;
                }
            }
            catch (OperationCanceledException)
            {
                _output.WriteLine("Cancelled.");
                return Cancelled;
            }
        }

        if (parsed.CaseFolders.Count > 1)
        {
            _output.WriteLine(
                $"{parsed.CaseFolders.Count - failures} of {parsed.CaseFolders.Count} case(s) completed.");
        }

        return failures == 0 ? Ok : Failed;
    }

    private async Task<bool> RunOneAsync(
        IServiceProvider provider,
        AppSettings settings,
        HostArguments parsed,
        string caseFolder,
        CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(caseFolder);
        var reference = parsed.CaseReference ?? new DirectoryInfo(full).Name;

        if (!Directory.Exists(full))
        {
            _output.WriteLine($"{reference}: the case folder \"{full}\" does not exist.");
            return false;
        }

        // The live settings instance every stage reads, so the case identity a stage filters on is
        // the case being run rather than whichever one was last opened in the desktop app.
        settings.CaseReference = reference;
        settings.DocumentFolder = full;

        var runFolder = RunFolder(parsed.RunRoot, full, reference);

        _output.WriteLine($"{reference}: {full}");
        _output.WriteLine($"  run folder: {runFolder}");

        if (parsed.DryRun)
        {
            var shape = provider.GetRequiredService<AgentWorkflowRunner>().Waves();

            for (var i = 0; i < shape.Count; i++)
            {
                _output.WriteLine($"  wave {i + 1}: {string.Join(" | ", shape[i].Select(s => s.Id))}");
            }

            return true;
        }

        var context = new WorkflowContext(full, reference, settings, runFolder)
        {
            Bypassed = parsed.SkipConversion
                ? new HashSet<WorkflowStageId> { WorkflowStageId.Convert }
                : [],
        };

        var progress = parsed.Quiet
            ? null
            : new Progress<StageProgress>(p => _output.WriteLine($"  {p}"));

        var result = parsed.Sequential
            ? await provider.GetRequiredService<WorkflowRunner>()
                .RunAsync(context, progress, cancellationToken).ConfigureAwait(false)
            : await provider.GetRequiredService<AgentWorkflowRunner>()
                .RunAsync(context, progress, cancellationToken).ConfigureAwait(false);

        foreach (var outcome in result.Outcomes)
        {
            _output.WriteLine(
                $"  {outcome.Stage,-7} {outcome.Status,-9} "
                + $"{outcome.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture),7}s  "
                + outcome.Summary);
        }

        _output.WriteLine($"  manifest: {result.ManifestPath}");

        return result.Succeeded;
    }

    /// <summary>
    /// Where this run's artefacts go.
    ///
    /// <b>A new folder per run, named by when it started.</b> Overwriting the last run's folder
    /// would destroy the only record of the comparison the batch exists to make, and the timestamp
    /// is what lets four configurations of the same case sit beside each other and be told apart.
    ///
    /// The reference is part of the name only under a shared root, where several cases would
    /// otherwise collide within the same second — and it goes through the path guard, because it
    /// reaches this from a folder name and is therefore input.
    /// </summary>
    private static string RunFolder(string? runRoot, string caseFolder, string reference)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var root = runRoot is null
            ? Path.Combine(caseFolder, "runs")
            : Path.GetFullPath(runRoot);

        var name = runRoot is null ? $"run-{stamp}" : $"{Sanitise(reference)}-{stamp}";

        return PathGuard.Require(root, name, "run folder");
    }

    /// <summary>
    /// A case reference reduced to what a folder name may hold.
    ///
    /// The path guard would refuse a traversal outright; this is the gentler half — a reference
    /// with a colon or a slash in it is a real reference and should still produce a run.
    /// </summary>
    private static string Sanitise(string reference)
    {
        var cleaned = new string([.. reference.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)]).Trim('.', ' ');

        return cleaned.Length == 0 ? "case" : cleaned;
    }
}
