using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Forcing the gateway to generate rather than replay.
///
/// <b>This exists because a replay is invisible and has twice been scored as a measurement.</b>
/// Two runs of this case came back in 4.4 seconds carrying 149,582 output tokens apiece and were
/// analysed in full before anyone worked out that no model produces 33,858 tokens a second. An
/// extraction re-issued with byte-identical requests then returned four sections differing by up
/// to 8,684 characters — so a cached answer is not even a stable cached answer, and the canonical
/// model digest moved without a single input changing.
///
/// Nothing in the request can ask a gateway to skip its cache, so the only lever is the body.
/// These pin the three properties that lever has to have: off is untouched, on is different, and
/// two runs are different from each other.
/// </summary>
public class PromptCacheBypassTests
{
    private const string Prompt = "Assess G1.4 against the pack below.";

    /// <summary>
    /// The default path must be byte-identical to what the app sent before this existed. A
    /// bypass that always altered the prompt would silently cost every run its prefix cache.
    /// </summary>
    [Fact]
    public void OffLeavesThePromptExactlyAsItWas()
    {
        Assert.Equal(Prompt, PromptCacheBypass.Off.Apply(Prompt));
        Assert.Equal(Prompt, PromptCacheBypass.For(enabled: false).Apply(Prompt));
        Assert.False(PromptCacheBypass.For(enabled: false).IsEnabled);
    }

    [Fact]
    public void OnChangesThePrompt()
    {
        var bypass = PromptCacheBypass.For(enabled: true);

        Assert.True(bypass.IsEnabled);
        Assert.NotEqual(Prompt, bypass.Apply(Prompt));
    }

    /// <summary>
    /// <b>The property the whole mechanism is for.</b> Two runs must not be able to answer each
    /// other's prompts, or the second is a replay of the first and the bypass has done nothing.
    /// </summary>
    [Fact]
    public void TwoRunsSendDifferentPromptsForTheSameQuestion()
    {
        var first = PromptCacheBypass.New().Apply(Prompt);
        var second = PromptCacheBypass.New().Apply(Prompt);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// One nonce per run, not per call: within a run the marker must be constant, so the
    /// provider's prefix cache still covers the check header that opens every group's prompt.
    /// </summary>
    [Fact]
    public void OneRunSendsTheSameMarkerOnEveryCall()
    {
        var bypass = PromptCacheBypass.New();

        Assert.Equal(bypass.Apply("first"), bypass.Apply("first"));
        Assert.Contains(bypass.Marker!, bypass.Apply("second"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The marker goes at the end. Providers cache on a shared prefix, so a marker at the front
    /// would invalidate from the first token and lose the report that every extraction pass
    /// re-reads — turning a cheap bypass into a twelve-fold document re-read.
    /// </summary>
    [Fact]
    public void TheMarkerIsAppendedSoThePrefixIsUntouched()
    {
        var applied = PromptCacheBypass.New().Apply(Prompt);

        Assert.StartsWith(Prompt, applied, StringComparison.Ordinal);
    }

    /// <summary>
    /// The marker is labelled rather than left as a bare token. It is inside the prompt whether
    /// the model attends to it or not, and a naked hex string invites it to be read as a fact
    /// about the case.
    /// </summary>
    [Fact]
    public void TheMarkerSaysItIsNotPartOfTheCase()
    {
        var applied = PromptCacheBypass.New().Apply(Prompt);

        Assert.Contains("Run marker", applied, StringComparison.Ordinal);
        Assert.Contains("ignore it", applied, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Both states have to be legible on a run's configuration line.</summary>
    [Fact]
    public void BothStatesDescribeThemselves()
    {
        Assert.Contains("allowed", PromptCacheBypass.Off.Describe(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bypassed", PromptCacheBypass.New().Describe(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Off by default, and deliberately so. The prefix cache is what makes one model call per
    /// requirement affordable; paying full input rate on every run to prove a point is a real
    /// cost, and one nobody asked for.
    /// </summary>
    [Fact]
    public void TheSettingIsOffUntilSomeoneTurnsItOn()
    {
        Assert.False(new AppSettings().BypassResponseCache);
    }
}
