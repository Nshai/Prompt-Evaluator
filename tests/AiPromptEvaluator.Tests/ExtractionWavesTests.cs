using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The dependency waves the extraction runs its passes in. The passes within a wave run
/// concurrently and the waves run in order, so the waves are only correct if a pass never lands in
/// the same wave as — or earlier than — the pass defining an id it references.
///
/// <b>This is the guard on a silent failure.</b> The identifier table a pass is shown holds only
/// the ids adopted by passes that have already merged, and the extractor is told to omit a
/// reference not in the table (see <see cref="CanonicalModelIdentityRegistry"/>). So a referencing
/// pass placed too early does not error — it drops the cross-link, and CHK-006 is the check that
/// link exists. The waves are derived from the schema for exactly this reason, and these tests hold
/// the derivation to the property it has to have.
/// </summary>
public class ExtractionWavesTests
{
    private static readonly string Schema = File.ReadAllText(LocateSchema());

    /// <summary>
    /// The property that owns each kind's collection, so a test can name the section that defines it
    /// without repeating the registry's own paths.
    /// </summary>
    private static int DefinerIndexOf(EntityKind kind)
    {
        var sections = ExtractionSection.All;
        var root = kind.CollectionPath[0];

        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Properties.Contains(root, StringComparer.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int WaveIndexOf(IReadOnlyList<IReadOnlyList<ExtractionSection>> waves, ExtractionSection section)
    {
        for (var w = 0; w < waves.Count; w++)
        {
            if (waves[w].Contains(section))
            {
                return w;
            }
        }

        return -1;
    }

    /// <summary>
    /// The property that makes the whole thing safe: every section is in a strictly later wave than
    /// the section defining any kind it references. Read from the schema the same way the waves are,
    /// so the two cannot silently drift apart.
    /// </summary>
    [Fact]
    public void EverySectionRunsAfterTheSectionsDefiningTheIdsItReferences()
    {
        var waves = ExtractionSection.Waves(Schema);

        for (var i = 0; i < ExtractionSection.All.Count; i++)
        {
            var section = ExtractionSection.All[i];
            var slice = JsonSchemaSlicer.Slice(Schema, section.Properties);
            var sectionWave = WaveIndexOf(waves, section);

            foreach (var kind in CanonicalModelIdentityRegistry.Kinds)
            {
                var definer = DefinerIndexOf(kind);

                // A back-edge to a kind defined later is the objective/recommendation cycle, which
                // the sequential order never resolved either — it is not a wait this pass observes.
                if (definer < 0 || definer >= i)
                {
                    continue;
                }

                var references = kind.ReferenceKeys.Any(key => slice.Contains($"\"{key}\"", StringComparison.Ordinal));
                if (!references)
                {
                    continue;
                }

                var definerWave = WaveIndexOf(waves, ExtractionSection.All[definer]);

                Assert.True(
                    definerWave < sectionWave,
                    $"\"{section.Name}\" references {kind.Name} (defined by "
                    + $"\"{ExtractionSection.All[definer].Name}\") but runs in wave {sectionWave}, "
                    + $"not after the defining wave {definerWave}.");
            }
        }
    }

    /// <summary>
    /// Case and parties runs first and alone. It defines the clients almost everything references,
    /// and it is the pass that warms the provider's prefix cache over the report — running it beside
    /// others would make each of them re-bill the document.
    /// </summary>
    [Fact]
    public void CaseAndPartiesIsTheWholeOfWaveZero()
    {
        var waves = ExtractionSection.Waves(Schema);

        var first = Assert.Single(waves[0]);
        Assert.Equal("Case and parties", first.Name);
    }

    /// <summary>The self-report pass runs last and alone, because it summarises the whole model the others built.</summary>
    [Fact]
    public void TheExtractionReportPassIsTheWholeOfTheLastWave()
    {
        var waves = ExtractionSection.Waves(Schema);

        var last = Assert.Single(waves[^1]);
        Assert.Equal("Triggers and extraction report", last.Name);
    }

    /// <summary>Every pass appears exactly once: the waves are a partition of the passes, not a filter of them.</summary>
    [Fact]
    public void EveryPassAppearsInExactlyOneWave()
    {
        var waves = ExtractionSection.Waves(Schema);
        var flattened = waves.SelectMany(w => w).ToList();

        Assert.Equal(ExtractionSection.All.Count, flattened.Count);
        Assert.Equal(ExtractionSection.All.OrderBy(s => s.Name), flattened.OrderBy(s => s.Name));
    }

    /// <summary>
    /// The wave membership for the current schema, pinned. This is not asserting the numbers are
    /// right in the abstract — the topological test above does that — but that a schema change which
    /// moves a pass between waves is noticed rather than shipped silently. The critical path is five
    /// waves deep against twelve passes run in series.
    /// </summary>
    [Fact]
    public void TheCurrentSchemaGivesTheExpectedFiveWaves()
    {
        var waves = ExtractionSection.Waves(Schema)
            .Select(w => w.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal).ToList())
            .ToList();

        Assert.Equal(5, waves.Count);

        Assert.Equal(["Case and parties"], waves[0]);
        Assert.Equal(["Financial position", "Objectives"], waves[1]);
        Assert.Equal(
            [
                "Disclosures",
                "Existing arrangements",
                "Needs analysis and research",
                "Replacement analysis",
                "Risk, knowledge and vulnerability",
                "Solution",
            ],
            waves[2]);
        Assert.Equal(["Costs and charges", "Recommendations"], waves[3]);
        Assert.Equal(["Triggers and extraction report"], waves[4]);
    }

    private static string LocateSchema()
    {
        var deployed = Path.Combine(AppContext.BaseDirectory, AppSettings.DefaultCanonicalSchemaFileName);
        if (File.Exists(deployed))
        {
            return deployed;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "artifacts", AppSettings.DefaultCanonicalSchemaFileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the canonical model schema for the test.");
    }
}
