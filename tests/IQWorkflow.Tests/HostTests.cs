using System.IO;

using AiPromptEvaluator;

using IQWorkflow;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The unattended entry point.
///
/// <b>This is the layer a scheduler talks to, so its contract is the exit code and the words it
/// writes</b> — not an object it returns. A host that ran the wrong case, silently ignored a
/// mistyped flag, or reported success for a failed batch would be wrong in a way nothing downstream
/// could detect, because the only thing watching is a cron entry reading one integer.
///
/// Every test here runs the whole host — parsing, batching, run folders, exit codes — over fake
/// stages, with no model, no vector store and no network.
/// </summary>
public sealed class HostTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-host", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakeStage(
        WorkflowStageId id,
        StageStatus status = StageStatus.Succeeded,
        params WorkflowStageId[] dependsOn) : IWorkflowStage
    {
        public WorkflowStageId Id => id;

        public IReadOnlyList<WorkflowStageId> DependsOn => dependsOn;

        /// <summary>The case references this stage was asked to run, in order.</summary>
        public List<string> Ran { get; } = [];

        public Task<bool> IsSatisfiedAsync(WorkflowContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<StageOutcome> ExecuteAsync(
            WorkflowContext context,
            IProgress<StageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Ran.Add(context.CaseReference);

            return Task.FromResult(status == StageStatus.Failed
                ? StageOutcome.Failed(id, $"{id} failed.")
                : StageOutcome.Succeeded(id, $"{id} did its work.", TimeSpan.FromMilliseconds(1)));
        }
    }

    /// <summary>
    /// A host over the stages a test supplies, with the settings a test supplies. The real
    /// composition root is not used, which is the point of injecting both.
    /// </summary>
    private static (UnattendedHost Host, StringWriter Output) Host(
        AppSettings settings, params IWorkflowStage[] stages)
    {
        var output = new StringWriter();

        var services = new ServiceCollection();

        foreach (var stage in stages)
        {
            services.AddSingleton(stage);
        }

        services.AddSingleton<AgentWorkflowRunner>();
        services.AddSingleton<WorkflowRunner>();

        var provider = services.BuildServiceProvider();

        // An empty environment, so what the machine running the tests happens to have exported
        // cannot change what these runs are configured with.
        return (new UnattendedHost(output, () => settings, _ => provider, _ => null), output);
    }

    private string CaseFolder(string name) =>
        Directory.CreateDirectory(Path.Combine(_folder, "cases", name)).FullName;

    // ──────────────────────────────────────────────
    // Parsing
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>The single most important property of an unattended tool.</b> A flag that is quietly
    /// ignored produces a run against settings nobody chose, a manifest that looks healthy, and a
    /// comparison between two configurations that were in fact the same one.
    /// </summary>
    [Fact]
    public void AnUnrecognisedArgumentIsAnErrorRatherThanIgnored()
    {
        var parsed = HostArguments.Parse(["--case", "x", "--fast"]);

        Assert.Contains("--fast", parsed.Error);
    }

    [Theory]
    [InlineData("--case")]
    [InlineData("--runs")]
    [InlineData("--reference")]
    [InlineData("--set")]
    public void AFlagMissingItsValueIsAnError(string flag) =>
        Assert.NotNull(HostArguments.Parse([flag]).Error);

    [Fact]
    public void NoCaseIsAnError() =>
        Assert.Contains("No case", HostArguments.Parse(["--quiet"]).Error);

    /// <summary>
    /// One reference across several cases would file every case's model, index and findings under
    /// the same identity — and the second case would find the first's work and skip its own.
    /// </summary>
    [Fact]
    public void OneReferenceCannotNameSeveralCases() =>
        Assert.NotNull(HostArguments.Parse(["--case", "a", "--case", "b", "--reference", "TC-1"]).Error);

    [Fact]
    public void TheFlagsAreRead()
    {
        var parsed = HostArguments.Parse([
            "--case", "a", "--runs", "r", "--sequential", "--quiet", "--dry-run", "--no-convert",
            "--set", "MaxPassagesPerGroup=12",
        ]);

        Assert.Null(parsed.Error);
        Assert.Equal(["a"], parsed.CaseFolders);
        Assert.Equal("r", parsed.RunRoot);
        Assert.True(parsed.Sequential);
        Assert.True(parsed.Quiet);
        Assert.True(parsed.DryRun);
        Assert.True(parsed.SkipConversion);
        Assert.Equal("12", parsed.Overrides["MaxPassagesPerGroup"]);
    }

    /// <summary>
    /// A batch's order is part of its reproducibility: two runs of the same root must produce
    /// manifests a reader can lay side by side.
    /// </summary>
    [Fact]
    public void ACasesRootExpandsToItsSubfoldersInOrder()
    {
        CaseFolder("TC-3");
        CaseFolder("TC-1");
        CaseFolder("TC-2");

        var parsed = HostArguments.Parse(["--cases-root", Path.Combine(_folder, "cases")]);

        Assert.Equal(
            ["TC-1", "TC-2", "TC-3"],
            parsed.CaseFolders.Select(c => new DirectoryInfo(c).Name));
    }

    // ──────────────────────────────────────────────
    // Settings on the command line
    // ──────────────────────────────────────────────

    [Fact]
    public void ASettingGivenOnTheCommandLineIsApplied()
    {
        var settings = new AppSettings();

        var errors = SettingsOverrides.Apply(settings, new Dictionary<string, string>
        {
            ["MaxPassagesPerGroup"] = "12",
            ["assertiondigest"] = "true",
            ["NearDuplicateOverlap"] = "0.75",
            ["QdrantCollection"] = "experiment",
        });

        Assert.Empty(errors);
        Assert.Equal(12, settings.MaxPassagesPerGroup);
        Assert.True(settings.AssertionDigest);
        Assert.Equal(0.75, settings.NearDuplicateOverlap);
        Assert.Equal("experiment", settings.QdrantCollection);
    }

    /// <summary>
    /// <b>A command line is public.</b> It is readable by every other process on the machine,
    /// recorded in shell history, and echoed by most schedulers into their own logs. A key given
    /// this way is a key disclosed — so it is refused rather than ignored, which is the difference
    /// between an operator learning immediately and learning never.
    /// </summary>
    [Theory]
    [InlineData("OpenAiApiKey")]
    [InlineData("EmbeddingApiKey")]
    [InlineData("QdrantApiKey")]
    public void ACredentialCannotBeSetOnTheCommandLine(string name)
    {
        var settings = new AppSettings();

        var errors = SettingsOverrides.Apply(settings, new Dictionary<string, string> { [name] = "leaked" });

        Assert.Single(errors);
        Assert.Contains(name, errors[0]);

        // Refused means not applied. An error message beside a setting that took effect anyway
        // would be the worst of both.
        Assert.DoesNotContain(
            "leaked",
            typeof(AppSettings).GetProperty(name)!.GetValue(settings)?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// <b>An endpoint is not a credential.</b> These were refused for a while on the reasoning that
    /// they belong with keys, which was wrong twice over: a URL on a command line discloses nothing,
    /// and pointing a run at a different endpoint is one of the things an unattended host most
    /// obviously exists to do.
    /// </summary>
    [Theory]
    [InlineData("OpenAiBaseUrl", "https://bedrock-runtime.eu-west-1.amazonaws.com")]
    [InlineData("QdrantEndpoint", "http://localhost:6334")]
    [InlineData("DoclingEndpoint", "http://localhost:5001")]
    public void AnEndpointCanBeSetOnTheCommandLine(string name, string value)
    {
        var settings = new AppSettings();

        Assert.Empty(SettingsOverrides.Apply(settings, new Dictionary<string, string> { [name] = value }));
        Assert.Equal(value, typeof(AppSettings).GetProperty(name)!.GetValue(settings));
    }

    /// <summary>
    /// A budget that counts tokens is not a token. The name match is deliberately broad, so the
    /// settings that would be caught by it are exempted by name rather than by narrowing it.
    /// </summary>
    [Fact]
    public void ATokenBudgetIsNotMistakenForACredential()
    {
        var settings = new AppSettings();

        Assert.Empty(SettingsOverrides.Apply(settings, new Dictionary<string, string>
        {
            ["MaxTokens"] = "2048",
            ["MaxTokensPerChunk"] = "500",
            ["ExtractionMaxTokens"] = "16000",
        }));

        Assert.Equal(2048, settings.MaxTokens);
        Assert.Equal(500, settings.MaxTokensPerChunk);
        Assert.Equal(16000, settings.ExtractionMaxTokens);
    }

    // ──────────────────────────────────────────────
    // Credentials from the environment
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>The route a scheduler actually has.</b> The command line refuses credentials and the
    /// settings file is a file a batch host may not have, so this is the supported way in.
    /// </summary>
    [Fact]
    public void AKeyInTheEnvironmentIsUsed()
    {
        var settings = new AppSettings { OpenAiApiKey = "from-the-settings-file" };

        var used = CredentialEnvironment.Apply(settings, name =>
            name == CredentialEnvironment.ChatKey ? "from-the-environment" : null);

        // The environment wins: a machine that exported a key was deliberately configured, usually
        // by whatever scheduled the run, and a key in a file written months ago is likelier stale.
        Assert.Equal("from-the-environment", settings.OpenAiApiKey);
        Assert.Equal([CredentialEnvironment.ChatKey], used);
    }

    /// <summary>Exporting one key must not blank the other two.</summary>
    [Fact]
    public void AnAbsentVariableLeavesItsSettingAlone()
    {
        var settings = new AppSettings { EmbeddingApiKey = "kept", QdrantApiKey = "also kept" };

        var used = CredentialEnvironment.Apply(settings, name =>
            name == CredentialEnvironment.ChatKey ? "chat" : "   ");

        Assert.Equal("kept", settings.EmbeddingApiKey);
        Assert.Equal("also kept", settings.QdrantApiKey);
        Assert.Equal([CredentialEnvironment.ChatKey], used);
    }

    /// <summary>The variable's name is worth logging when a run fails to authenticate; the key is not.</summary>
    [Fact]
    public async Task TheHostNamesTheVariableAndNeverTheKey()
    {
        var output = new StringWriter();
        var services = new ServiceCollection();

        services.AddSingleton<IWorkflowStage>(new FakeStage(WorkflowStageId.Assess));
        services.AddSingleton<AgentWorkflowRunner>();
        services.AddSingleton<WorkflowRunner>();

        var host = new UnattendedHost(
            output,
            () => new AppSettings(),
            _ => services.BuildServiceProvider(),
            name => name == CredentialEnvironment.ChatKey ? "sk-secret-value" : null);

        await host.RunAsync(["--case", CaseFolder("TC-1")]);

        Assert.Contains(CredentialEnvironment.ChatKey, output.ToString());
        Assert.DoesNotContain("sk-secret-value", output.ToString());
    }

    [Theory]
    [InlineData("MaxPassagesPerGroup", "twelve")]
    [InlineData("AssertionDigest", "yes")]
    [InlineData("NearDuplicateOverlap", "high")]
    [InlineData("NotASetting", "1")]
    public void ASettingThatCannotBeUsedIsReportedRatherThanGuessed(string name, string value) =>
        Assert.Single(SettingsOverrides.Apply(new AppSettings(), new Dictionary<string, string> { [name] = value }));

    /// <summary>
    /// Nothing runs when a flag was wrong. A benchmark run configured by six flags of which five
    /// landed is a measurement of a configuration nobody chose.
    /// </summary>
    [Fact]
    public async Task ARejectedSettingStopsTheRunBeforeAnythingHappens()
    {
        var stage = new FakeStage(WorkflowStageId.Assess);
        var (host, output) = Host(new AppSettings(), stage);

        var code = await host.RunAsync(["--case", CaseFolder("TC-1"), "--set", "OpenAiApiKey=leaked"]);

        Assert.Equal(UnattendedHost.Unusable, code);
        Assert.Empty(stage.Ran);
        Assert.DoesNotContain("leaked", output.ToString());
    }

    // ──────────────────────────────────────────────
    // Running
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ACaseRunsAndTheHostReportsSuccess()
    {
        var stage = new FakeStage(WorkflowStageId.Assess);
        var settings = new AppSettings();
        var (host, output) = Host(settings, stage);
        var folder = CaseFolder("TC-1");

        var code = await host.RunAsync(["--case", folder, "--quiet"]);

        Assert.Equal(UnattendedHost.Ok, code);
        Assert.Equal(["TC-1"], stage.Ran);

        // The folder name becomes the reference, and the live settings carry it — every stage and
        // the vector store filter on this, so a host that left the last-opened case in place would
        // index one case's documents under another's identity.
        Assert.Equal("TC-1", settings.CaseReference);
        Assert.Equal(folder, settings.DocumentFolder);

        Assert.Contains("Assess", output.ToString());
        Assert.Contains(WorkflowManifest.FileName, output.ToString());
    }

    /// <summary>The manifest is the audit trail; a run that leaves none cannot be resumed or read.</summary>
    [Fact]
    public async Task ARunLeavesAManifestUnderTheRunFolder()
    {
        var (host, _) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Assess));
        var runs = Path.Combine(_folder, "runs");

        Assert.Equal(
            UnattendedHost.Ok,
            await host.RunAsync(["--case", CaseFolder("TC-1"), "--runs", runs, "--quiet"]));

        var manifests = Directory.GetFiles(runs, WorkflowManifest.FileName, SearchOption.AllDirectories);
        var manifest = WorkflowManifest.Read(Assert.Single(manifests));

        Assert.Equal("TC-1", manifest!.CaseReference);
    }

    /// <summary>
    /// Two runs of one case must not overwrite each other: the earlier manifest is the only record
    /// of the comparison a batch exists to make.
    /// </summary>
    [Fact]
    public async Task TwoRunsOfOneCaseGetTheirOwnFolders()
    {
        var (host, _) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Assess));
        var runs = Path.Combine(_folder, "runs");
        string[] args = ["--case", CaseFolder("TC-1"), "--runs", runs, "--quiet"];

        await host.RunAsync(args);

        // A run folder is named by the second it started in, so a second run within the same second
        // would legitimately reuse the folder. Naming the folder is what is under test, not the
        // clock, so the two are separated.
        await Task.Delay(1100);
        await host.RunAsync(args);

        Assert.Equal(2, Directory.GetDirectories(runs).Length);
    }

    /// <summary>
    /// <b>A batch does not stop at its first bad case.</b> One unreadable report abandoning the
    /// other seven turns one problem into a wasted night — and the exit code still has to say that
    /// something went wrong.
    /// </summary>
    [Fact]
    public async Task ABatchRunsEveryCaseAndStillReportsTheFailure()
    {
        var extract = new FakeStage(WorkflowStageId.Extract);
        var (host, output) = Host(new AppSettings(), extract, new FailOnFirstCase());

        var code = await host.RunAsync([
            "--case", CaseFolder("TC-1"), "--case", CaseFolder("TC-2"), "--quiet",
        ]);

        Assert.Equal(UnattendedHost.Failed, code);
        Assert.Equal(["TC-1", "TC-2"], extract.Ran);
        Assert.Contains("1 of 2 case(s) completed.", output.ToString());
    }

    private sealed class FailOnFirstCase : IWorkflowStage
    {
        public WorkflowStageId Id => WorkflowStageId.Assess;

        public IReadOnlyList<WorkflowStageId> DependsOn => [];

        public Task<bool> IsSatisfiedAsync(WorkflowContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<StageOutcome> ExecuteAsync(
            WorkflowContext context,
            IProgress<StageProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(context.CaseReference == "TC-1"
                ? StageOutcome.Failed(Id, "The report could not be read.")
                : StageOutcome.Succeeded(Id, "Assessed.", TimeSpan.Zero));
    }

    [Fact]
    public async Task ACaseFolderThatDoesNotExistFailsThatCaseAndIsNamed()
    {
        var (host, output) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Assess));

        var code = await host.RunAsync(["--case", Path.Combine(_folder, "absent"), "--quiet"]);

        Assert.Equal(UnattendedHost.Failed, code);
        Assert.Contains("does not exist", output.ToString());
    }

    /// <summary>The shape, without spending anything to see it.</summary>
    [Fact]
    public async Task ADryRunPrintsTheWavesAndRunsNothing()
    {
        var stage = new FakeStage(WorkflowStageId.Assess, StageStatus.Succeeded, WorkflowStageId.Extract);
        var (host, output) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Extract), stage);

        var code = await host.RunAsync(["--case", CaseFolder("TC-1"), "--dry-run"]);

        Assert.Equal(UnattendedHost.Ok, code);
        Assert.Empty(stage.Ran);
        Assert.Contains("wave 1: Extract", output.ToString());
        Assert.Contains("wave 2: Assess", output.ToString());
    }

    [Fact]
    public async Task HelpIsNotAnError()
    {
        var (host, output) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Assess));

        Assert.Equal(UnattendedHost.Ok, await host.RunAsync(["--help"]));
        Assert.Contains("--cases-root", output.ToString());
    }

    // ──────────────────────────────────────────────
    // Bypassing conversion
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Every stored case is already Markdown</b>, and a benchmark batch over them should not ask
    /// the sidecar about a case file nobody has changed. The stage keeps its place in the graph, so
    /// its dependants still wait for it — what they need is the artefact, which is already there.
    /// </summary>
    [Fact]
    public async Task NoConvertBypassesConversionAndRunsEverythingElse()
    {
        var convert = new FakeStage(WorkflowStageId.Convert);
        var extract = new FakeStage(WorkflowStageId.Extract, StageStatus.Succeeded, WorkflowStageId.Convert);

        var (host, output) = Host(new AppSettings(), convert, extract);

        var code = await host.RunAsync(["--case", CaseFolder("TC-1"), "--no-convert", "--quiet"]);

        Assert.Equal(UnattendedHost.Ok, code);
        Assert.Empty(convert.Ran);
        Assert.Equal(["TC-1"], extract.Ran);
        Assert.Contains("Bypassed", output.ToString());
    }

    /// <summary>
    /// Bypassing is recorded as a bypass and not as "already current". They are different facts:
    /// one is the stage's own judgement about its output, the other is an instruction that
    /// overrode it — and when a case turns out not to have been converted after all, this line is
    /// the only record of why the run went wrong.
    /// </summary>
    [Fact]
    public async Task ABypassedStageSaysSoInTheManifest()
    {
        var (host, _) = Host(new AppSettings(), new FakeStage(WorkflowStageId.Convert));
        var runs = Path.Combine(_folder, "runs");

        await host.RunAsync(["--case", CaseFolder("TC-1"), "--runs", runs, "--no-convert", "--quiet"]);

        var manifest = WorkflowManifest.Read(
            Directory.GetFiles(runs, WorkflowManifest.FileName, SearchOption.AllDirectories).Single());

        var record = Assert.Single(manifest!.Stages);

        Assert.Equal(StageStatus.Skipped, record.Status);
        Assert.Contains("Bypassed", record.Summary);
    }

    /// <summary>Both runners honour it, or the flag means one thing with the engine and another without.</summary>
    [Fact]
    public async Task TheSequentialRunnerBypassesToo()
    {
        var convert = new FakeStage(WorkflowStageId.Convert);
        var (host, _) = Host(new AppSettings(), convert, new FakeStage(WorkflowStageId.Assess));

        Assert.Equal(
            UnattendedHost.Ok,
            await host.RunAsync(["--case", CaseFolder("TC-1"), "--no-convert", "--sequential", "--quiet"]));

        Assert.Empty(convert.Ran);
    }

    /// <summary>Without the flag, the stage runs. The bypass is opt-in and nothing else changed.</summary>
    [Fact]
    public async Task ConversionRunsWhenItIsNotBypassed()
    {
        var convert = new FakeStage(WorkflowStageId.Convert);
        var (host, _) = Host(new AppSettings(), convert);

        await host.RunAsync(["--case", CaseFolder("TC-1"), "--quiet"]);

        Assert.Equal(["TC-1"], convert.Ran);
    }
}
