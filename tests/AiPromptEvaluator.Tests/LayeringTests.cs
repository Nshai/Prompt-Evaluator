using System.Reflection;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The pipeline must not know what is drawing it.
///
/// The point of splitting the assembly was to make the desktop app one possible front end rather
/// than the only one. That property is easy to state and easy to lose: one `MessageBox` for a
/// warning, one `Color` on a status, one method taking a `Control` to marshal onto, and the core
/// can no longer be referenced by a web API or a CLI.
///
/// A test is the only thing that keeps it true, because nothing else fails when it stops being
/// true — the solution still compiles, because the desktop app is still there.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Core = typeof(CheckPlanRunner).Assembly;

    [Fact]
    public void TheCoreAssemblyIsNotTheDesktopApp()
    {
        Assert.Equal("AiPromptEvaluator.Core", Core.GetName().Name);
    }

    /// <summary>
    /// No reference to WinForms or to the drawing stack, direct or transitive-by-name. Both are
    /// separate assemblies even inside a Windows-targeted library, so a reference is visible
    /// here the moment one is taken.
    /// </summary>
    [Theory]
    [InlineData("System.Windows.Forms")]
    [InlineData("System.Drawing")]
    [InlineData("System.Drawing.Common")]
    public void TheCoreDoesNotReferenceAUiFramework(string forbidden)
    {
        var referenced = Core.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain(forbidden, referenced);
    }

    /// <summary>
    /// And no reference to the app either, which would be the same coupling arriving the other
    /// way round.
    /// </summary>
    [Fact]
    public void TheCoreDoesNotReferenceTheApp()
    {
        Assert.DoesNotContain(
            "AiPromptEvaluator",
            Core.GetReferencedAssemblies().Select(a => a.Name));
    }

    /// <summary>
    /// The prompts are the specification the model works to, so they are worth pinning: an empty
    /// or truncated constant would produce a run that looks normal and assesses nothing
    /// properly.
    /// </summary>
    [Fact]
    public void BothSystemPromptsSurvivedBeingMoved()
    {
        Assert.Contains("financial services Quality Assurance assessor", Prompts.AssessorSystem);
        Assert.Contains("Decide last", Prompts.AssessorSystem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Where the evidence is a TABLE", Prompts.AssessorSystem);

        Assert.Contains("canonical JSON model", Prompts.ExtractorSystem);
        Assert.Contains("Never invent a value", Prompts.ExtractorSystem);
        Assert.Contains("Record contradictions rather than resolving them", Prompts.ExtractorSystem);
    }
}
