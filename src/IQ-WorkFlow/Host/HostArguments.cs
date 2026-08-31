using System.IO;

namespace IQWorkflow;

/// <summary>
/// The command line, parsed.
///
/// <b>Separated from the host that acts on it so it can be tested without running anything.</b>
/// Argument parsing is where an unattended tool most often goes quietly wrong — a mistyped flag that
/// is silently ignored produces a run against the wrong settings and a manifest that looks perfectly
/// healthy. Every unrecognised token is therefore an error, never a shrug.
/// </summary>
public sealed record HostArguments
{
    /// <summary>The case folders to run, in the order they were given.</summary>
    public IReadOnlyList<string> CaseFolders { get; init; } = [];

    /// <summary>
    /// Where run folders are created. Null means beside each case, under <c>runs</c>, which keeps a
    /// case's artefacts with the case they describe.
    /// </summary>
    public string? RunRoot { get; init; }

    /// <summary>
    /// The case reference, where the operator supplies one rather than taking the folder name.
    ///
    /// Only meaningful for a single case: one reference across a batch would file every case's
    /// model, index and findings under the same identity.
    /// </summary>
    public string? CaseReference { get; init; }

    /// <summary>
    /// Settings named on the command line, overriding the stored ones for this invocation only.
    ///
    /// <b>This is what makes the 2×2 experiment a script rather than a person.</b> Four assessment
    /// configurations over one extraction is four invocations differing by one flag; without it,
    /// it is four visits to a settings dialog with no record of what was set.
    /// </summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Drive the stages sequentially, without the Agent Framework engine.</summary>
    public bool Sequential { get; init; }

    /// <summary>Suppress per-stage progress. Stage outcomes are still reported.</summary>
    public bool Quiet { get; init; }

    /// <summary>Resolve and print what would run, and run nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Run without performing conversion.
    ///
    /// <b>For a case whose documents are already Markdown</b> — which is every stored test case, and
    /// how a benchmark batch is normally kept. Conversion would find nothing to do and say so, but
    /// only after enumerating the case file and asking the sidecar about it; the flag is the
    /// operator saying they already know.
    ///
    /// The stage still holds its place in the graph, so extraction and indexing wait for it exactly
    /// as before. Only the work is skipped, and the manifest records that it was bypassed rather
    /// than that it was current.
    /// </summary>
    public bool SkipConversion { get; init; }

    public bool HelpRequested { get; init; }

    /// <summary>Why the command line could not be used. Null when it can.</summary>
    public string? Error { get; init; }

    public const string Usage = """
        IQWorkflow — runs a case through the evaluation pipeline, unattended.

          IQWorkflow --case <folder> [--case <folder> ...] [options]
          IQWorkflow --cases-root <folder> [options]

        Cases
          --case <folder>        A case folder. Repeatable; cases run one after another.
          --cases-root <folder>  Run every immediate subfolder of <folder> as a case.
          --reference <ref>      The case reference. Defaults to the folder name.
                                 Valid only with a single case.

        Run
          --runs <folder>        Where run folders are created. Defaults to <case>\runs.
          --set Name=Value       Override one setting for this invocation. Repeatable.
                                 Credentials cannot be set this way; see below.
          --no-convert           Do not convert; the case documents are already Markdown.
          --sequential           Drive the stages in sequence rather than as a graph.
          --dry-run              Print what would run, and run nothing.
          --quiet                Suppress per-stage progress.
          --help                 This text.

        Credentials (environment only — a command line is public)
          IQFLOW_LLM_API_KEY        the chat service's key
          IQFLOW_EMBEDDING_API_KEY  the embedding service's key, if it is a different service
          IQFLOW_QDRANT_API_KEY     a secured Qdrant's key
          Any of these overrides the settings file. Endpoints and models are ordinary settings
          and can be given with --set.

        Exit codes
          0  every case completed
          1  at least one case failed
          2  the command line or the settings could not be used
          130 cancelled
        """;

    /// <summary>
    /// Parses, and reports the first problem rather than the last.
    ///
    /// <b>Nothing here touches the file system beyond expanding <c>--cases-root</c>.</b> Whether a
    /// case folder holds anything usable is the conversion stage's judgement, and duplicating it
    /// here would be a second opinion to keep in step with the first.
    /// </summary>
    public static HostArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var cases = new List<string>();
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? runRoot = null;
        string? reference = null;
        var sequential = false;
        var quiet = false;
        var dryRun = false;
        var skipConversion = false;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];

            switch (token)
            {
                case "--help" or "-h" or "-?" or "/?":
                    return new HostArguments { HelpRequested = true };

                case "--sequential":
                    sequential = true;
                    break;

                case "--quiet":
                    quiet = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--no-convert":
                    skipConversion = true;
                    break;

                case "--case":
                    if (Next(args, ref i) is not { } caseFolder)
                    {
                        return Fail("--case needs a folder.");
                    }

                    cases.Add(caseFolder);
                    break;

                case "--cases-root":
                    if (Next(args, ref i) is not { } root)
                    {
                        return Fail("--cases-root needs a folder.");
                    }

                    if (!Directory.Exists(root))
                    {
                        return Fail($"The cases root \"{root}\" does not exist.");
                    }

                    // Ordered by name rather than by whatever the file system returns, so a batch
                    // runs in the same order twice and two manifests can be read side by side.
                    cases.AddRange(Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal));
                    break;

                case "--runs":
                    if (Next(args, ref i) is not { } runs)
                    {
                        return Fail("--runs needs a folder.");
                    }

                    runRoot = runs;
                    break;

                case "--reference":
                    if (Next(args, ref i) is not { } named)
                    {
                        return Fail("--reference needs a value.");
                    }

                    reference = named;
                    break;

                case "--set":
                    if (Next(args, ref i) is not { } assignment)
                    {
                        return Fail("--set needs Name=Value.");
                    }

                    var split = assignment.IndexOf('=');

                    if (split <= 0 || split == assignment.Length - 1)
                    {
                        return Fail($"--set expects Name=Value; got \"{assignment}\".");
                    }

                    // Last wins, and silently: a script that computes its own flags may repeat one,
                    // and refusing would make the caller deduplicate what the parser can.
                    overrides[assignment[..split].Trim()] = assignment[(split + 1)..].Trim();
                    break;

                default:
                    return Fail($"Unrecognised argument \"{token}\".");
            }
        }

        if (cases.Count == 0)
        {
            return Fail("No case was given. Use --case or --cases-root.");
        }

        if (reference is not null && cases.Count > 1)
        {
            return Fail(
                "--reference names one case, and " + cases.Count + " were given. Every case would "
                + "otherwise be indexed and stored under the same reference.");
        }

        return new HostArguments
        {
            CaseFolders = cases,
            RunRoot = runRoot,
            CaseReference = reference,
            Overrides = overrides,
            Sequential = sequential,
            Quiet = quiet,
            DryRun = dryRun,
            SkipConversion = skipConversion,
        };
    }

    private static string? Next(IReadOnlyList<string> args, ref int i) =>
        i + 1 < args.Count ? args[++i] : null;

    private static HostArguments Fail(string error) => new() { Error = error };
}
