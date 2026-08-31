using Microsoft.Agents.AI.Workflows;

namespace IQWorkflow;

/// <summary>
/// Builds the pipeline as an Agent Framework workflow graph, from the dependencies the stages
/// declare about themselves.
///
/// <b>The graph is derived, not drawn.</b> A hand-drawn graph is a second statement of the
/// pipeline's shape that has to be kept in step with the stages, and the two would disagree the
/// first time a dependency changed. Each stage says what it needs; the shape follows.
///
/// <b>Waves become fan-out and fan-in.</b> Stages whose dependencies are all satisfied run in the
/// same superstep, which is what makes extraction and indexing overlap — the two bill against
/// different services and sequencing them spends the longer of the two twice over. Where a stage
/// depends on several, a join node collapses the branches so the stage node itself stays uniform.
/// </summary>
public sealed class CaseWorkflowGraph
{
    private readonly IReadOnlyList<IWorkflowStage> _stages;

    public CaseWorkflowGraph(IEnumerable<IWorkflowStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);

        _stages = [.. stages];

        if (_stages.Count == 0)
        {
            throw new ArgumentException("A workflow needs at least one stage.", nameof(stages));
        }

        var duplicates = _stages
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToString())
            .ToList();

        // Two stages sharing an id would also share a node id in the graph, so an event, a
        // checkpoint and a manifest row would all name something ambiguous.
        if (duplicates.Count > 0)
        {
            throw new ArgumentException(
                $"Two stages share the id {string.Join(", ", duplicates)}. A stage id is its node id "
                + "in the graph and its key in the manifest, and has to be unique.", nameof(stages));
        }
    }

    /// <summary>
    /// The stages grouped into supersteps: everything in a wave is independent of everything else
    /// in it, and depends only on stages in earlier waves.
    ///
    /// Exposed because it is the pipeline's shape and worth asserting on directly — a change that
    /// serialises what should overlap is otherwise only visible as a run that takes twice as long
    /// for no stated reason.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<IWorkflowStage>> Waves()
    {
        var waves = new List<IReadOnlyList<IWorkflowStage>>();
        var placed = new HashSet<WorkflowStageId>();
        var remaining = _stages.ToList();

        while (remaining.Count > 0)
        {
            var wave = remaining.Where(s => s.DependsOn.All(placed.Contains)).ToList();

            if (wave.Count == 0)
            {
                // Every remaining stage waits on something that will never arrive. Named rather
                // than deadlocked: a workflow that hangs tells nobody which dependency was wrong.
                throw new InvalidOperationException(
                    "The stages cannot be ordered: "
                    + string.Join(", ", remaining.Select(s => $"{s.Id} depends on "
                        + string.Join("+", s.DependsOn.Where(d => !placed.Contains(d)))))
                    + ". A dependency names a stage that is not registered, or two stages depend on "
                    + "each other.");
            }

            waves.Add(wave);
            remaining.RemoveAll(wave.Contains);

            foreach (var stage in wave)
            {
                placed.Add(stage.Id);
            }
        }

        return waves;
    }

    /// <summary>
    /// The graph, ready to run.
    /// </summary>
    /// <param name="progress">Relayed to each stage; the framework's events describe the graph, not the work.</param>
    public Workflow Build(IProgress<StageProgress>? progress = null)
    {
        var waves = Waves();

        var start = new StartNode();
        var nodes = _stages.ToDictionary(
            s => s.Id, s => new StageExecutorNode(s, progress));

        var builder = new WorkflowBuilder(start);

        // The first wave hangs off the start node. Fan-out where there is more than one, because a
        // single-target fan-out is just an edge and reads worse.
        Connect(builder, start, waves[0].Select(s => nodes[s.Id]).ToList());

        for (var i = 1; i < waves.Count; i++)
        {
            foreach (var stage in waves[i])
            {
                var target = nodes[stage.Id];
                var sources = stage.DependsOn
                    .Select(d => (ExecutorBinding)nodes[d])
                    .ToList();

                if (sources.Count == 1)
                {
                    builder.AddEdge(sources[0], target);
                }
                else
                {
                    // A barrier so the stage starts once, after every dependency has finished,
                    // rather than once per dependency. The join collapses the branches back to one
                    // signal so the stage node keeps the same shape as every other.
                    var join = new JoinNode($"join-{stage.Id}");

                    builder.AddFanInBarrierEdge(sources, join);
                    builder.AddEdge(join, target);
                }
            }

        }

        // No output node is declared. A stage raises its outcome as an event rather than returning
        // it, so everything the run produces arrives on the event stream — and a graph whose result
        // depended on a terminal node would have no result at all whenever a stage failed and its
        // branch stopped short, which is exactly the run whose record matters most.
        return builder.WithName("iq-workflow-case").Build();
    }

    private static void Connect(
        WorkflowBuilder builder, StartNode source, IReadOnlyList<StageExecutorNode> targets)
    {
        if (targets.Count == 1)
        {
            builder.AddEdge(source, targets[0]);

            return;
        }

        builder.AddFanOutEdge(source, targets.Select(t => (ExecutorBinding)t).ToList());
    }
}
