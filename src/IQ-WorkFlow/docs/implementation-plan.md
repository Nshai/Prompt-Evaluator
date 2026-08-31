# IQ-WorkFlow — implementation plan

**Reads with** [pipeline-approach-analysis.md](../../../docs/Architecture/pipeline-approach-analysis.md)
(Revision 4) and [pipeline-implementation-plan.md](../../../docs/Architecture/pipeline-implementation-plan.md).
2026-08-31.

---

## 0. The objective

**Minimum cost, maximum findings against the expected benchmark, in the least wall-clock time.**

Everything below is judged against that, and the three parts are not independent — a configuration
that halves cost and loses a third of the findings is worse, not cheaper. What the workflow
contributes to each:

| Objective | What the workflow does | What it cannot do |
| --- | --- | --- |
| **Cost** | Reuses a stored canonical model rather than re-extracting — the largest saving on any repeated run, and what makes four assessment configurations over one extraction affordable. Skips satisfied stages. Records cost per stage so it is a number rather than an impression. | Change what a run spends on the work it actually does. That is `Core`'s, and the parent plan's Moves 3–8. |
| **Findings** | Scores every run against the case benchmark, so hits are an output rather than a study. Refuses to assess without a model, and reports a check with no plan rather than skipping it. | Improve recall. Every finding is `Core`'s. Automating a 75% run makes it 75% more often. |
| **Time** | Runs independent stages concurrently — extraction and indexing overlap, and they bill against different services so neither waits on the other's rate limit. Resumes rather than restarts. | Make a model call faster. |

**The two halves have to be measured together.** A run that reports only its cost has measured the
half that means nothing alone: £3.00 is cheap or ruinous depending entirely on what it found. That
is why scoring is a stage and not a follow-up.

---

## 1. What this is, and what it is not

**IQ-WorkFlow runs the v1 pipeline end to end, without a person.** Every stage it drives already
exists and is already tested; what does not exist is a way to run them in order, repeatably,
unattended, and to know afterwards exactly what happened.

Today that orchestration lives in WinForms event handlers — `LoadDocsButton_Click`,
`ExtractModelButton_Click`, `RunButton_Click`. The sequence is real and correct, and it is
unreachable by anything that is not a person clicking. A benchmark run of the 2×2 that Phase 1 needs
is eight runs of four manual steps, each with its own opportunity to differ from the last in a way
nobody records.

**It is not a new pipeline.** It adds no stage, changes no prompt, and makes no retrieval or
assessment decision. The one exception is stated and bounded in §5.

### The boundary, stated plainly

| | |
| --- | --- |
| **In scope** | Sequencing, resumption, run manifests, cost accounting, an unattended entry point, and the ingestion fixes named in §5. |
| **Out of scope** | Anything in `pipeline-approach-analysis.md` §6.2's bottom limb — document claims, path-equality reconciliation, per-candidate adjudication. Those are v2's, and [pipeline-implementation-plan.md](../../../docs/Architecture/pipeline-implementation-plan.md) §0 records why they are not v1's. |
| **Explicitly forbidden** | Importing v2's design. IQ-Flow is a different pipeline; borrowing its architecture here would fork this one. Ingestion is the single exception and it is a set of defect fixes, not a design. |

---

## 2. The stages, and where each already lives

The analysis names the stages; every one of them is implemented in `AiPromptEvaluator.Core`. This
project composes them and owns nothing else.

| Stage | What it does | Implemented by | Owned here |
| :-: | --- | --- | :-: |
| **S0** | Case documents → Markdown | `DocumentToPdfConverter`, `DoclingClient` | sequencing + §5 fixes |
| **S1** | Report → canonical model (12 LLM passes) | `ICanonicalModelExtractor` | sequencing |
| **S2** | Chunk and embed → Qdrant | `ICaseDocumentIndexer`, `ICaseDocumentStore` | sequencing |
| **S2a** | The check plans | `CheckQueryPlanLoader`, `CheckPlanLint` | load + refuse a bad plan |
| **S3/S4** | Retrieve and assess, one call per requirement | `ICheckPlanRunnerFactory` | sequencing + bounded fan-out |
| **S5** | Verify citations, aggregate, report | `CheckFinding`, `ComplianceReportHtml`, `ICheckRunStore` | sequencing + artefact write |

**S1 and S2 are independent of each other** and both depend only on S0. They are still run in
sequence rather than concurrently: they contend for the same rate-limited endpoints, and a run whose
cost is being measured should not have two stages billing at once.

---

## 3. Design

### 3.1 The shape

A **pipeline of stages** over a shared context, each stage a strategy behind one interface:

```
IWorkflowStage
   ExecuteAsync(WorkflowContext, IProgress<StageProgress>, CancellationToken) -> StageOutcome

WorkflowRunner
   for each stage in order:
       if manifest says the stage is already satisfied for this input -> skip
       else execute, record outcome, persist manifest
       if outcome is a failure the pipeline cannot continue past -> stop
```

Chosen because the stages genuinely are a linear pipeline with a shared, growing context, and
because it makes the two properties that matter cheap: **a stage can be skipped when its work is
already done**, and **a run can be resumed** at the first stage that is not.

### 3.2 The principles, and where each one bites

- **SRP** — one stage, one job, one reason to change. A stage that both indexed and assessed would
  have to be re-run whole when either changed.
- **DIP** — every stage depends on a `Core` abstraction (`ICaseDocumentIndexer`,
  `ICanonicalModelExtractor`, `ICheckPlanRunnerFactory`), never a concrete. That is what lets the
  tests drive the whole pipeline with no Qdrant, no model and no network.
- **OCP** — a new stage is a new registration, not an edit to the runner.
- **DRY** — this project reimplements nothing that `Core` already does. Where a behaviour is
  missing, it is added to `Core` and used here, rather than forked.
- **YAGNI** — no stage abstraction beyond what the six stages need. No plug-in discovery, no
  scheduler, no distributed anything. The manifest is a file, not a database, because one process
  writes it.

### 3.3 Patterns used, and why

| Pattern | Where | What it buys |
| --- | --- | --- |
| Pipeline / Chain | `WorkflowRunner` over `IWorkflowStage` | Ordering and resumption in one place |
| Strategy | Each stage | A stage is swappable and independently testable |
| Template Method | `WorkflowStage` base | Timing, cancellation and failure handling written once |
| Factory | `Core`'s existing factories | Per-run objects (stores, runners) without service-locator |
| Options | `WorkflowOptions` | Configuration validated once, at startup, not at first use |
| Result object | `StageOutcome` | A stage reports failure without throwing for control flow |

### 3.4 Non-functional requirements

These are the properties a compliance tool is judged on, so they are requirements and not
aspirations.

| NFR | How it is met | How it is proven |
| --- | --- | --- |
| **Reproducibility** | The run fingerprint (`RunFingerprint`) is captured in the manifest; sampling is pinned; results are collected by index, never appended | A test asserts two runs of the same input produce the same manifest fingerprint |
| **Resumability** | Each stage records an input digest; an unchanged input is skipped | A test resumes a half-finished manifest and asserts only the remaining stages run |
| **Observability** | Every stage reports progress and writes a timed, costed outcome to the manifest | The manifest is the audit trail; a failed run says which stage and why |
| **Cost control** | Token usage is accumulated per stage from `Core`'s own usage tracking | The manifest carries per-stage cost; a run that spends nothing reports nothing rather than £0.00 |
| **Bounded resource use** | `ConcurrencyGate` bounds model and search calls run-wide, not per stage | Existing `Core` tests; the runner shares one gate across stages |
| **Fail-safe** | A stage failure stops the pipeline before a later stage reads a half-written artefact | A test asserts a failing stage prevents its successors from running |
| **Security** | §4 | §4 |

### 3.5 Secure coding

Handling client financial files unattended, so these are not optional.

- **No secrets anywhere durable.** The manifest records the run fingerprint and never an API key,
  endpoint credential or connection string. `AppSettings.NotFingerprinted` already names the
  credentials; the manifest writer honours the same list rather than keeping a second one.
- **Path containment.** Every path derived from configuration or a case folder is resolved to a
  full path and checked to be inside its permitted root before it is read or written. A case folder
  is user input, and `..` in it is a directory traversal.
- **No partial artefacts.** Outputs are written to a temporary file and moved into place, so a
  cancelled run cannot leave a half-written report that reads as a complete one.
- **Cancellation is honoured everywhere**, so an operator stopping a run stops it rather than
  detaching from it.
- **Least surprise on failure.** An error names the stage, the input and the fix; it never echoes a
  credential and never includes the case's own content.

---

## 4. Ingestion — the one permitted exception

`pipeline-approach-analysis.md` names a conversion defect that nothing downstream can recover:

> Fix the DOCX table loss at S0 — **a hard floor of 4 findings nothing downstream can recover.**

Four fixes are ported from `IQFlow.Adapters/Ingest`. **They are defect fixes, not a design import**,
and each is here because the analysis names the defect it repairs:

| Fix | Defect it repairs |
| --- | --- |
| `DocxTableRecovery` | Tables lost entirely in DOCX conversion — the hard floor of 4 findings |
| `TableGridBuilder`, `TableNarration` | A table segment without its header row is *"a list of numbers nobody can interpret"* |
| `CheckboxNormaliser` | `- [x] xInternal transfer` — the OCR glyph bleeding into the label, on the Pension Sharing Annex that carries the most severe finding of Test Case 3 |
| `EmailDocumentConverter` | Correspondence dates: the recommendation meeting scheduled for `30th March`, which appears 44 times in the extract and is the largest single miss bucket in §2 |
| `PictureDescriber` | Figures that exist only as images |

**The two narration passes are off by default, and that is part of the port rather than a
deviation from it.** `TableNarration` and `PictureDescriber` are the only parts of conversion that
spend money — a model call per table, a vision call per picture — and the reference implementation
ships both disabled for the same reason. `AppSettings.TableNarration` and
`AppSettings.PictureNarration` switch them on; both are fingerprinted, because a run with either on
converts to a different document. A run left at the defaults makes no model call during conversion
at all, and therefore needs no chat credential to convert a case.

Two cost guards sit with picture transcription: `MinimumImageBytes` (4096, below the measured
content floor of ~19 KB and above the ~2 KB decoration) and `MaxImagesPerDocument` (40), so a slide
deck cannot run away with a run.

**What is deliberately not ported:** anything that changes the *shape* of what conversion produces
for the rest of the pipeline. The downstream contract is unchanged — a folder of Markdown, one file
per source document, in category subfolders. Chunking, indexing, retrieval and assessment cannot
tell that conversion improved, only that there is more in the file that was always supposed to be
there.

---

## 5. Build order

Each step is independently useful and independently tested. Nothing later is required for anything
earlier to be worth having.

| # | Step | Why here |
| :-: | --- | --- |
| 1 | Abstractions, context, manifest, runner, DI | Nothing can be tested until the frame exists |
| 2 | Stage: index (S2) | The cheapest real stage; proves the frame against `Core` |
| 3 | Stage: extract (S1) | The expensive stage; proves cost accounting and resumption |
| 4 | Stage: assess (S3/S4) | The stage the whole thing exists to reach |
| 5 | Stage: report (S5) | Makes a run's output an artefact rather than a log |
| 6 | Stage: convert (S0) + ingestion fixes | Last because it is the only stage carrying new logic, and §4's fixes are worth their own attention |
| 7 | Unattended host | The entry point that makes the workflow autonomous |

The host is `IQWorkflow.exe` — the library's own executable, because a separate project holding a
ten-line `Main` would be a project to version, package and reference for no gain. It takes one or
more cases, batches them, gives each its own run folder and manifest, and returns an exit code a
scheduler can read. Settings can be varied per invocation with `--set Name=Value`, which is what
makes the 2×2 experiment a script; credentials cannot, because a command line is public. Conversion
can be bypassed with `--no-convert` for a case that is already Markdown, which every stored case is.

Progress is tracked in [status-tracker.md](status-tracker.md), which is updated as each step lands
rather than at the end.

---

## 6. What this does not solve

Stated so it is not mistaken for solved.

- **The Phase 1 gate is still unanswered.** This makes the experiment cheap to run; it does not run
  it, and it does not predict the result.
- **It does not improve coverage.** Every finding a run produces is produced by `Core`. If recall is
  75%, automating the run makes it 75% more often.
- **Configuration still has to be right.** The workflow validates what it can — endpoints reachable,
  plans loadable, dimensions matching the collection — and refuses to start rather than failing
  halfway. It cannot know that a model id is the wrong one for an endpoint until the endpoint says
  so.
