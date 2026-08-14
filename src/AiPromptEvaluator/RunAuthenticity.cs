namespace AiPromptEvaluator;

/// <summary>
/// Whether a run's output could physically have been generated in the time the run took.
///
/// This exists because of an observed failure, not a hypothetical one. A gateway served a
/// cached response for a whole run: sixty findings and 83,000 output tokens came back in
/// <b>four seconds</b>, byte-identical to a run from the previous day, and the report carried a
/// fresh timestamp and a fresh cost line. Two separate analyses drew conclusions from replays
/// before anyone noticed, and eight of the twelve run logs on record turned out to be replays.
///
/// A replay is indistinguishable from a generated run by content — that is what makes it
/// dangerous. It reproduces failures as faithfully as successes, so it looks like evidence.
/// What it cannot fake is the clock. The application has no response cache of its own, and
/// prefix caching reduces the cost of *reading* input, never of *writing* output, so output
/// tokens per second is a physical rate.
///
/// The observed bands are far apart, which is what makes the test safe:
///
/// <code>
///   generated runs   322 – 647 tok/s      (five runs, two configurations, groups in parallel)
///   cache replays  20,000 – 49,000 tok/s  (seven runs)
/// </code>
///
/// The threshold sits in the empty space between them, three times above the fastest run
/// actually observed and an order of magnitude below the slowest replay.
/// </summary>
public sealed record RunAuthenticity(long OutputTokens, TimeSpan Elapsed)
{
    /// <summary>
    /// Output tokens per second above which a run is treated as a replay.
    ///
    /// Deliberately generous. Raising group concurrency raises the aggregate rate, so the point
    /// is not to model the fastest legitimate run — it is to sit in the two-order-of-magnitude
    /// gap between generating text and replaying it. A run that trips this has not been slow;
    /// it has been impossible.
    /// </summary>
    public const double ImplausibleTokensPerSecond = 2_000;

    /// <summary>A run too short or too small to say anything about.</summary>
    public static readonly RunAuthenticity Unknown = new(0, TimeSpan.Zero);

    /// <summary>
    /// True when the duration was measured and cannot account for the output. False when the
    /// rate is plausible, and false when there is nothing to judge — an unmeasured run is not
    /// accused.
    /// </summary>
    public bool IsReplay =>
        Elapsed > TimeSpan.Zero
        && OutputTokens > 0
        && TokensPerSecond > ImplausibleTokensPerSecond;

    public double TokensPerSecond =>
        Elapsed > TimeSpan.Zero ? OutputTokens / Elapsed.TotalSeconds : 0;

    /// <summary>
    /// The banner for the head of a report. Says what happened, what it means, and what not to
    /// do with the run — a reader who has never heard of this failure has to be able to act on
    /// one line.
    /// </summary>
    public string Banner =>
        $"*** REPLAY — NOT A GENERATED RUN. {OutputTokens:N0} output tokens in "
        + $"{Elapsed.TotalSeconds:0.#}s is {TokensPerSecond:N0} tok/s, which no model produces. "
        + "The gateway served a cached response. Findings, timings and cost below are a "
        + "recording of an earlier run; do not read them as evidence of anything that changed "
        + "since. ***";
}
