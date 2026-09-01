# IQ-WorkFlow — status tracker

Updated as each step lands. Companion to [implementation-plan.md](implementation-plan.md) §5.

**Build:** clean, warnings-as-errors · **Tests:** `IQWorkflow.Tests` 139 passed ·
`AiPromptEvaluator.Tests` 1,101 passed (unchanged by this work).

---

## Build order

| # | Step | Status | Tests | Notes |
| :-: | --- | :-: | :-: | --- |
| 1 | Abstractions, context, manifest, runner, DI | **Done** | 34 | Drives the whole pipeline with no Qdrant, model or network |
| 1a | Agent Framework execution engine | **Done** | 7 | Graph derived from declared dependencies; tested on the real engine |
| 2 | Stage: index (S2) | **Done** | 5 | Skips on unchanged input; refuses when the store is down or nothing indexed |
| 3 | Stage: extract (S1) | **Done** | 3 | Reuses a stored model; fails fast with no report in the folder |
| 4 | Stage: assess (S3/S4) | **Implemented, not yet tested** | 0 | Bounded fan-out; refuses without a model. Needs six fakes — the last stage without cover |
| 5 | Stage: report (S5) | **Done** | 2 | Atomic write; refuses without an assessed run |
| 5a | Stage: score against benchmark | **Done** | 3 | Added for the objective: a run reports hits, not only cost |
| 6 | Stage: convert (S0) + ingestion fixes | **Done** | 20 | v2's fix pipeline, v1's Markdown output contract |
| 6a | Email, picture and table narration | **Done** | 34 | The last three §4 fixes; both narration passes off by default |
| 7 | Unattended host | **Done** | 31 | `IQWorkflow.exe`: batching, run folders, exit codes, `--set`, `--no-convert` |

**"Implemented, not yet tested" means exactly that.** A stage in that state compiles against `Core`'s
interfaces and its logic is written, and no test drives it. Its behaviour is therefore unverified —
including the failure paths, which is where most of its code is. **Assessment is the one stage still
in that state**, and it is the most expensive stage in the pipeline.

Writing the stage tests found a real defect: `ScoreBenchmarkStage` wrote its worksheet without
ensuring the run folder existed, relying on the runner having made it first. A stage that only works
because something else happened to run before it cannot be run on its own, and would have failed the
first time anyone invoked it directly.

---

## Running it

```
IQWorkflow --case <folder> [--case <folder> ...] [options]
IQWorkflow --cases-root <folder> [options]
```

`--reference <ref>` (one case only) · `--runs <folder>` · `--set Name=Value` · `--no-convert` ·
`--sequential` · `--dry-run` · `--quiet` · `--help`.

Exit codes: `0` every case completed · `1` at least one failed · `2` the command line or the
settings could not be used · `130` cancelled.

The 2×2 experiment is four invocations of the same batch differing by one `--set`:

```
IQWorkflow --cases-root cases --runs runs\digest-on  --no-convert --set AssertionDigest=true
IQWorkflow --cases-root cases --runs runs\digest-off --no-convert --set AssertionDigest=false
```

Credentials are not settable this way. They come from the settings file and the environment.

---

## Non-functional requirements

Each is a claim, and each names what proves it. **A row without a test is a design intent, not a
property.**

| NFR | Status | Proven by |
| --- | :-: | --- |
| Reproducibility — same configuration, same digest | **Met** | `ManifestAndGuardTests.TheFingerprintIsStableAcrossRuns` |
| Reproducibility — a configuration change starts fresh | **Met** | `WorkflowRunnerTests.AConfigurationChangeStartsAFreshManifest` |
| Security — no credential reaches the manifest | **Met** | `ManifestAndGuardTests.NoCredentialReachesTheManifest` |
| Security — path containment | **Met** | `ManifestAndGuardTests` (10 cases, including the sibling-prefix boundary) |
| Resumability — a satisfied stage is skipped | **Met** | `WorkflowRunnerTests.ASatisfiedStageIsSkipped` |
| Resumability — a run resumes where it stopped | **Met** | `WorkflowRunnerTests.AResumedRunSkipsWhatAlreadySucceeded` |
| Resumability — a retry replaces its record | **Met** | `WorkflowRunnerTests.ARetriedStageReplacesItsRecord` |
| Fail-safe — a failure stops the successors | **Met** | `WorkflowRunnerTests.AFailedStageStopsThePipeline` |
| Fail-safe — a defect still produces a manifest | **Met** | `WorkflowRunnerTests.AThrowingStageBecomesARecordedFailure` |
| Observability — every stage recorded, in order | **Met** | `WorkflowRunnerTests.EveryStageIsRecordedInOrder` |
| Durability — a manifest reads back whole | **Met** | `ManifestAndGuardTests.AWrittenManifestReadsBackWhole` |
| Durability — an unreadable manifest is treated as absent | **Met** | `ManifestAndGuardTests.AnUnreadableManifestIsTreatedAsAbsent` |
| Cost — unknown cost is not reported as zero | **Met** | `ManifestAndGuardTests.AnUnreportedCostIsNotZero` |
| Cancellation is honoured between stages | **Met** | `WorkflowRunnerTests.CancellationStopsBetweenStages` |
| Time — independent stages share a wave | **Met** | `WorkflowRunnerTests.IndependentStagesShareAWave` |
| Time — concurrent stages actually overlap | **Met** | `WorkflowRunnerTests.ConcurrentStagesActuallyOverlap` |
| Time — an unsatisfiable dependency is named, not hung | **Met** | `WorkflowRunnerTests.AnUnsatisfiableDependencyIsNamed` |
| Engine — stages overlap in one superstep | **Met** | `AgentWorkflowRunnerTests.IndependentStagesOverlapInOneSuperstep` |
| Engine — a fan-in target runs exactly once | **Met** | `AgentWorkflowRunnerTests.AFanInTargetRunsExactlyOnce` |
| Engine — a failure stops its dependants | **Met** | `AgentWorkflowRunnerTests.AFailedStageStopsItsDependants` |
| Engine — the manifest survives the run | **Met** | `AgentWorkflowRunnerTests.TheManifestIsOnDiskAfterTheRun` |
| **Bounded resource use — one gate run-wide** | **Intended, unproven** | Written in `AssessChecksStage`; no test yet |
| No partial artefacts — atomic write | **Met** | `ManifestAndGuardTests.AWrittenManifestReadsBackWhole` and `StageTests.ReportWritesItsArtefactsIntoTheRunFolder` (no `.tmp` survives) |
| Cost — a stage reports what it indexed and billed | **Met** | `StageTests.IndexReportsWhatItIndexed` |
| **Cost — assessment's own accounting** | **Intended, unproven** | Wired in `AssessChecksStage`; no test yet |
| Security — a credential cannot be passed on the command line | **Met** | `HostTests.ACredentialOrEndpointCannotBeSetOnTheCommandLine` (5 cases) |
| Autonomy — a mistyped flag stops the run | **Met** | `HostTests.AnUnrecognisedArgumentIsAnErrorRatherThanIgnored` |
| Autonomy — the exit code reports the batch | **Met** | `HostTests.ABatchRunsEveryCaseAndStillReportsTheFailure` |
| Autonomy — one bad case does not abandon the batch | **Met** | `HostTests.ABatchRunsEveryCaseAndStillReportsTheFailure` |
| Reproducibility — two runs of a case keep both records | **Met** | `HostTests.TwoRunsOfOneCaseGetTheirOwnFolders` |
| Observability — every run leaves a manifest | **Met** | `HostTests.ARunLeavesAManifestUnderTheRunFolder` |
| Time — conversion can be bypassed on a converted case | **Met** | `HostTests.NoConvertBypassesConversionAndRunsEverythingElse` (and the sequential runner) |
| **Cost — conversion spends nothing by default** | **Met** | `EmailAndNarrationTests.NothingIsSentToAModelUnlessNarrationIsSwitchedOn` |
| Cost — a decorative picture is not sent to a vision model | **Met** | `EmailAndNarrationTests.ADecorativeImageIsNotWorthAVisionCall` |
| Cost — a signature-sized attachment is not converted | **Met** | `EmailAndNarrationTests.ASignatureSizedAttachmentIsNotWorthARoundTrip` |
| Reproducibility — narration changes the fingerprint | **Met** | `EmailAndNarrationTests.SwitchingNarrationOnChangesTheRunFingerprint` |
| Fidelity — narration never drops a value | **Met** | `EmailAndNarrationTests.AValueTheModelDroppedIsPutBackVerbatim` |
| Fidelity — the table itself survives narration | **Met** | `EmailAndNarrationTests.ANarratedTableIsAddedBesideTheTableRatherThanInsteadOfIt` |
| Security — an attachment name cannot escape its folder | **Met** | `EmailAndNarrationTests.AnAttachmentNameCannotEscapeItsFolder` (3 cases) |
| Fail-safe — an unreadable message is one document's problem | **Met** | `EmailAndNarrationTests.AnUnreadableMessageIsRecordedRatherThanThrown` |

---

## Decisions taken

Recorded so they are not silently revisited.

**S1 and S2 run concurrently. — Reversed 2026-08-31.**

The earlier decision was to run them in sequence, on the grounds that they contend for the same
rate-limited endpoints and that a run whose cost is being measured should not have two stages
billing at once. **Both halves of that reasoning were wrong.** Extraction bills the chat endpoint and
indexing bills the embedding endpoint — on this deployment those are different Bedrock services with
different quotas, so they do not contend. And cost is accounted per stage from each stage's own
usage tracking, so concurrency does not blur the attribution.

What remained was the cost of the decision: extraction alone is 300–630 seconds on a measured case,
and sequencing spends the shorter of the two twice over. With wall-clock time an explicit objective,
that is not defensible.

Stages therefore declare `DependsOn`, and the runner executes dependency-respecting waves
concurrently. Extraction and indexing share the first wave; assessment depends on both.

**The execution engine is Microsoft Agent Framework (`Microsoft.Agents.AI.Workflows`).**

The stages are unchanged and know nothing about it — `StageExecutorNode` is the whole of the
framework's reach into the pipeline, which is why every stage test still runs with no engine at all.
What the framework supplies is the graph, the supersteps that make independent stages overlap, the
event stream and the checkpointing; each of those is something this project would otherwise have
written and then had to test, and the concurrency is where a hand-rolled version is most likely to
be subtly wrong.

**This is not v2's design.** The instruction to build against v1 stands: no stage was added, removed
or reordered, and nothing from the v2 pipeline came with it. An execution engine is a technology
choice, not an architecture.

`WorkflowRunner`, the plain sequential implementation, is kept and still registered. It needs no
engine, which makes it the cheaper thing to reach for when diagnosing whether a problem is in a
stage or in the orchestration.

**Conversion uses v2's implementation, and v1's output contract.**

The fix pipeline is ported from `IQFlow.Adapters.Ingest` — `DoclingClient` (which returns the JSON
as well as the Markdown, where v1's returns Markdown alone), `FormFieldReconstructor`,
`MarkdownBackfiller`, `CheckboxNormaliser`, `DocxTableRecovery`, `TableGridBuilder`, `DoclingModels`
— byte-faithful apart from the namespace. A tidied copy would be a second implementation to keep in
step with the original, and these are the product of measurement rather than of taste.

**What did not come across is v2's data model.** Its converter returns an `IngestedDocument` of
segments; this emits Markdown into category folders with a `_conversion-summary.json` beside them,
because that is the shape every converted case on disk already has and what v1's indexer, extractor
and stored runs read. Importing segments would have been importing the other pipeline.

The order of the passes is v2's and is not arbitrary: reconstruct form fields, backfill from the
JSON, *then* normalise checkboxes — reconstruction is what produces the checkbox lines, so
normalising earlier finds nothing to clean — and finally recover `.docx` tables the converter never
returned.

**A document that fails to convert does not fail the run.** Thirty-seven documents where one is an
encrypted PDF should produce thirty-six conversions and a summary line saying why the
thirty-seventh is missing. `DocumentUnreadableException` was ported for the same reason: a coverage
gap over a document nobody could open reads downstream as "the case file does not evidence this",
which is a claim about the advice when it is really a claim about the run.

**The host is the library's own executable, and everything in it is injected.**

`OutputType` is `Exe` and `Program.Main` is fifteen lines — the console, the Ctrl+C handler and the
exit code, which are the only parts no test can reach. Everything else is `UnattendedHost`, which
takes its output writer, its settings loader and its container as constructor parameters. That is
what lets parsing, batching, run-folder naming, credential refusal and exit codes be driven by
tests over fake stages, with no model, no vector store and no network. A separate project holding a
ten-line `Main` would have been a project to version, package and reference for no gain.

**An unrecognised argument stops the run.** For a tool a person watches, a shrug is survivable; for
one a scheduler runs at night, a silently ignored flag produces a run against settings nobody chose,
a manifest that looks perfectly healthy, and a comparison between two configurations that were in
fact the same one.

**`--set Name=Value` reaches any setting except a credential or an endpoint.** The 2×2 experiment is
four assessment configurations over one extraction; without this it is four visits to a settings
dialog with no record of what was set. Credentials are refused rather than ignored, because a
command line is readable by every other process on the machine, is kept in shell history, and is
echoed by most schedulers into their own logs — a key passed as an argument is a key disclosed. The
refusal matches on the name, so a credential added to `AppSettings` later is refused by default.

**`--no-convert` bypasses conversion without removing it from the graph.**

Every stored case is already Markdown, and conversion over one would find nothing to do only after
enumerating the case file and asking the sidecar about it. The flag is the operator saying they
already know.

The stage keeps its place in the graph and its dependants still wait for it, because what extraction
and indexing depend on is the *artefact* rather than the running — a converted case has everything
conversion would have produced. Removing the node would have meant rebuilding the edges and giving
the graph two shapes to test.

**It is recorded as a bypass, not as "already current".** Those are different facts — one is the
stage's own judgement about its output, the other is an instruction that overrode that judgement —
and on the day someone passes the flag against a case that was *not* converted, the manifest line is
the only record of why the run went wrong. Both runners honour it, or the flag would mean one thing
with the engine and another without.

**Email is a decorator over the converter, not a branch inside it.**

`EmailDocumentConverter` claims `.eml` and `.msg` — the two formats Docling rejects — reads them
with MimeKit and MsgReader, hands each attachment back to the real converter, and delegates
everything else untouched. Neither half knows anything about the other's problem, and the seam
(`ICaseDocumentConverter`, returning a bundle) exists because one file is not always one document.

**An attachment becomes a document in its own right**, named `<message>-<attachment>.md` in the same
category folder, carrying a provenance line naming the message and its date. Two messages in a
thread both carrying `statement.pdf` therefore do not write over each other. Attachments below 4 KB,
and the `.p7s`/`.ics`/`.gif` class, are not converted: they are signatures and tracking pixels, and
a round trip each yields a logo.

**The reading half is ported byte-faithfully and the writing half is not.** The reference
implementation returns segments; this emits Markdown, which is v1's contract. Everything that was
learned by measurement — inline parts as well as declared attachments (one 12 MB message in Test
Case 3 is a single inline JPEG that is the whole evidential content), the Windows-1252 code-page
registration without which `MsgReader` is permanently unusable, the header block as citable text —
came across as written.

**Table narration and picture transcription are off by default, and that is the point of them.**

They are the only parts of conversion that spend money: a model call per table, a vision call per
picture. `AppSettings.TableNarration` and `AppSettings.PictureNarration` switch them on, and a run
left at the defaults makes no model call while converting — so the default configuration converts a
case on a machine with only a Docling endpoint configured. Both settings are fingerprinted, because
a run with either on converts to a different document, and both require a re-convert and a re-index
to take effect.

**Narration appends; it never substitutes.** The grid stays exactly as the deterministic passes
produced it, because that is what the chunker keeps whole and what the extractor reads. Every
non-blank source cell missing from a model's narrative is appended verbatim under "Also noted" —
that check is the only reason narration is safe to offer on regulated material, since a silently
summarised-away charge is the class of loss this pipeline exists to catch rather than create. When
the model is unreachable the deterministic narrator takes over, which loses polish and no data.

**The narratives sit in an appendix rather than beside each table, which the reference version does
not do.** It works on segments and knows which narrative belongs to which table; this emits
Markdown, where pairing a JSON table with a rendered one means counting table blocks in text that is
*known* to drop tables — that is why `DocxTableRecovery` exists. A narrative attached to the wrong
table is worse than one in an appendix, so each block names its table index and repeats the column
headers.

**Dependencies are declared, not implied by registration order.** Registration order previously *was*
execution order, which made the sequence readable in one place but also made every stage implicitly
depend on all of its predecessors. Declaring the real dependency is what lets the runner overlap
what is genuinely independent, and it puts the ordering rule beside the stage that knows it.

**The manifest is a JSON file, not a database.** One process writes it, it is read by people as
often as by code, and `ICheckRunStore` already owns the durable record of what a run *found*. The
manifest records what a run *did*.

**A stage decides for itself whether it is satisfied.** The runner does not know what makes an index
current — only the index stage does. This keeps the resumption rule beside the work it describes,
and is why `IWorkflowStage` has `IsSatisfiedAsync` rather than the runner comparing timestamps.

**Assessment is never satisfied in advance.** An earlier run's findings are a different run's
findings. Treating them as current would make the workflow's purpose conditional on nobody having
run it before, and would make the noise floor — which no configuration on two of three cases has
ever had — impossible to measure.

**The index stage's satisfaction check is a count, not a content comparison.** Knowing whether the
index matches the folder exactly would mean hashing every document and keeping that in step with the
chunker's settings. A count answers the question a resumed run actually asks. An operator who has
changed the documents unloads the case, which is the existing gesture for exactly this.

**The workflow never repairs configuration.** It validates and refuses. A workflow that silently
corrected a dimension mismatch or created a missing collection would make a run's meaning depend on
state nobody recorded.

---

## Known gaps

Stated plainly rather than left to be discovered.

- **Neither narration pass has been run against a real model.** The tests drive both through fakes
  and a stubbed sidecar, which proves the wiring, the cost guards and the no-loss check — not that
  a vision model reads the Test Case 3 risk-profile bitmaps usefully. That is a measurement, and it
  needs one real run with `PictureNarration` on.
- **Assessment has no test.** It is the most expensive stage in the pipeline and the only one
  without cover; it needs six fakes, which is why it is last. This is the largest gap.
- **No stage has been run against real infrastructure.** What the tests prove is the logic and the
  sequencing, not the integration — the same gap the parent plan records for the assertion digest,
  and it closes the same way: one real run.
- **Cost is per stage, not per document.** Enough to compare two runs; not enough to answer which
  document was expensive.
- **The workflow inherits every open question in the parent plan**, including Phase 1's unanswered
  gate. Automating a run does not validate what the run does.
