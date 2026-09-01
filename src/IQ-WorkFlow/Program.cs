using System.IO;

namespace IQWorkflow;

/// <summary>
/// The process.
///
/// <b>Deliberately almost empty.</b> Everything a run does is in <see cref="UnattendedHost"/>, which
/// takes its output and its container as parameters and is therefore testable; what is left here is
/// the part that cannot be — the console, the Ctrl+C handler, and the exit code. Logic that lives in
/// <c>Main</c> is logic no test will ever reach.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var stopping = new CancellationTokenSource();

        // Cancelled rather than killed, so the manifest for the stage in flight is written and the
        // next attempt is a resumption. A second Ctrl+C is left to the runtime, which is the escape
        // hatch for a stage that will not stop.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stopping.Cancel();
        };

        try
        {
            return await new UnattendedHost(Console.Out).RunAsync(args, stopping.Token);
        }
        catch (OperationCanceledException)
        {
            return UnattendedHost.Cancelled;
        }
        catch (Exception ex)
        {
            // A stage failing is an outcome and is reported as one; reaching here means a defect,
            // so it is said plainly rather than dressed as a failed run.
            Console.Error.WriteLine($"The workflow host failed unexpectedly: {ex}");
            return UnattendedHost.Unusable;
        }
    }
}
