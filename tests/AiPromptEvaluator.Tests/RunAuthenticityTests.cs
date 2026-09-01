using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The guard against reading a gateway cache replay as a run.
///
/// Anchored on the two real logs rather than on invented numbers. `160653` generated sixty
/// findings in 122 seconds; the log stamped `20260814_114031` reproduced the same sixty
/// findings, byte for byte, in **four**. Both carried a fresh timestamp, a fresh cost line and
/// a configuration block that read identically, and the second was analysed twice before anyone
/// checked the clock.
/// </summary>
public class RunAuthenticityTests
{
    // Output tokens for the run in question. Both logs are the same sixty responses, so the
    // token count is the same and only the duration separates them — which is the whole point.
    private const long RunOutputTokens = 83_463;

    [Fact]
    public void AGeneratedRunIsNotAccused()
    {
        var generated = new RunAuthenticity(RunOutputTokens, TimeSpan.FromSeconds(122));

        Assert.False(generated.IsReplay);
        Assert.InRange(generated.TokensPerSecond, 600, 700);
    }

    [Fact]
    public void TheFourSecondReplayIsCaught()
    {
        var replay = new RunAuthenticity(RunOutputTokens, TimeSpan.FromSeconds(4));

        Assert.True(replay.IsReplay);
        Assert.Contains("REPLAY", replay.Banner);
        Assert.Contains("do not read them as evidence", replay.Banner);
    }

    /// <summary>
    /// The slowest run on record is 322 tok/s and the fastest 647. The threshold has to clear
    /// all of them with room for a concurrency change, and still sit far below the slowest
    /// replay observed at roughly 20,000 tok/s.
    /// </summary>
    [Theory]
    [InlineData(322)]
    [InlineData(590)]
    [InlineData(647)]
    [InlineData(1_999)]
    public void PlausibleRatesPass(double tokensPerSecond)
    {
        var run = new RunAuthenticity(
            (long)(tokensPerSecond * 100), TimeSpan.FromSeconds(100));

        Assert.False(run.IsReplay);
    }

    [Theory]
    [InlineData(20_866)]
    [InlineData(26_650)]
    [InlineData(49_058)]
    [InlineData(165_551)]
    public void ObservedReplayRatesFail(double tokensPerSecond)
    {
        var run = new RunAuthenticity(
            (long)(tokensPerSecond * 100), TimeSpan.FromSeconds(100));

        Assert.True(run.IsReplay);
    }

    /// <summary>
    /// A run built without a measured duration must not be accused. Reports assembled in tests
    /// and older call sites pass no clock, and silence is not evidence of fakery.
    /// </summary>
    [Fact]
    public void AnUnmeasuredRunIsNotAccused()
    {
        Assert.False(RunAuthenticity.Unknown.IsReplay);
        Assert.False(new RunAuthenticity(RunOutputTokens, TimeSpan.Zero).IsReplay);
        Assert.False(new RunAuthenticity(0, TimeSpan.FromSeconds(4)).IsReplay);
    }

    // ──────────────────────────────────────────────
    // The banner has to reach the report, not just the type
    // ──────────────────────────────────────────────

    [Fact]
    public void TheReportStampsAReplayAboveEverythingElse()
    {
        var report = ReportRunningFor(TimeSpan.FromSeconds(4));

        var text = report.Format();
        var banner = text.IndexOf("*** REPLAY", StringComparison.Ordinal);
        var heading = text.IndexOf("QA FINDINGS", StringComparison.Ordinal);

        Assert.True(banner >= 0, "the replay banner is missing from the report");
        Assert.True(banner < heading, "the banner must precede the findings heading");
        Assert.StartsWith("REPLAY (cached)", report.Headline);
    }

    [Fact]
    public void TheReportSaysNothingAboutAGeneratedRun()
    {
        var text = ReportRunningFor(TimeSpan.FromSeconds(122)).Format();

        Assert.DoesNotContain("*** REPLAY", text);
        Assert.Contains("tok/s", text); // the rate is still reported, so it can be audited
    }

    [Fact]
    public void AReportWithNoClockSaysNothingEitherWay()
    {
        var text = ReportRunningFor(TimeSpan.Zero).Format();

        Assert.DoesNotContain("*** REPLAY", text);
        Assert.DoesNotContain("Wall clock", text);
    }

    private static FindingsReport ReportRunningFor(TimeSpan duration) =>
        new(
            "ABC-99", 99, "intelliflo-claude-haiku-4-5", DateTimeOffset.Now,
            [
                new CheckFinding
                {
                    CheckId = "CHK-001",
                    CheckName = "Suitability completeness",
                    Outcome = nameof(CheckOutcome.PotentialConcern),
                    Usage = new TokenUsage(0, RunOutputTokens, 0, 0),
                },
            ],
            Model: null,
            Fingerprint: null,
            RunDuration: duration);
}
