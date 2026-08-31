using System.IO;
using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

public class AppSettings
{
    /// <summary>Where an OpenAI-compatible API lives by default. Any compatible gateway works.</summary>
    public const string DefaultBaseUrl = "https://api.openai.com/v1";

    /// <summary>Where the Docling container publishes its API by default.</summary>
    public const string DefaultDoclingEndpoint = "http://localhost:5001";

    /// <summary>Where a local Qdrant container publishes its gRPC API by default.</summary>
    public const string DefaultQdrantEndpoint = "http://localhost:6334";

    /// <summary>The collection every case document chunk is written to.</summary>
    public const string DefaultQdrantCollection = "case_documents";

    /// <summary>
    /// Settings that cannot move a finding, and are therefore left out of
    /// <see cref="RunFingerprint.SettingsDigest"/>.
    ///
    /// <b>The digest is deliberately opt-out rather than opt-in.</b> Listing what to include
    /// means a setting added next month is silently uncovered, which is exactly the failure the
    /// fingerprint exists to prevent: four settings that change which passages reach an assessor
    /// were absent from it when this was written, and two of them had been added that week. A run
    /// with core-only queries and a run with all of them retrieve different evidence and printed
    /// the same fingerprint, so anyone diffing them was comparing two pipelines while reading a
    /// line saying they were the same one.
    ///
    /// Opt-out inverts the cost: forgetting means an over-sensitive digest, which is noise a
    /// reader can see and dismiss. Forgetting the other way is a wrong answer nobody can see.
    ///
    /// Three kinds of thing are here, and only these three:
    ///
    /// <list type="bullet">
    /// <item><b>Credentials and endpoints</b> — where the service is and how to authenticate. The
    /// same run against the same models through a different URL is the same run.</item>
    /// <item><b>Case identity and storage paths</b> — which case, and where its artefacts live.
    /// The fingerprint's subject is everything the runner reads <i>other than</i> the case, and
    /// the case's own content already reaches it as the canonical model digest.</item>
    /// <item><b>Concurrency</b> — how long a run takes and not what it produces. That is a
    /// property the code works to preserve: results are written into arrays by index rather than
    /// appended, precisely so parallelism cannot reorder a pack. It stops being exempt the moment
    /// that stops being true.</item>
    /// </list>
    ///
    /// Anything else — a cap, a floor, a threshold, a scope toggle, a chunk size, a token budget
    /// — belongs in the digest, and the way to keep it out is to argue it into one of the three
    /// categories above rather than to add a name here.
    /// </summary>
    public static readonly IReadOnlySet<string> NotFingerprinted = new HashSet<string>(StringComparer.Ordinal)
    {
        // Credentials and endpoints.
        nameof(OpenAiApiKey),
        nameof(OpenAiBaseUrl),
        nameof(EmbeddingApiKey),
        nameof(EmbeddingBaseUrl),
        nameof(QdrantApiKey),
        nameof(QdrantEndpoint),
        nameof(DoclingEndpoint),
        nameof(AvailableModels),

        // Case identity and storage paths.
        nameof(CaseReference),
        nameof(TenantId),
        nameof(DocumentFolder),
        nameof(QdrantCollection),
        nameof(CanonicalModelDbPath),
        nameof(CanonicalSchemaPath),
        nameof(CheckPlanFolder),
        nameof(CheckRunDbPath),
        nameof(PromptLogFolder),
        nameof(LastChecksCsvPath),

        // Concurrency.
        nameof(MaxParallelRequests),
        nameof(MaxParallelChecks),
    };

    [JsonPropertyName("openAiApiKey")]
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the OpenAI-compatible endpoint, e.g. https://api.openai.com/v1 or a
    /// self-hosted gateway. Leave empty to use <see cref="DefaultBaseUrl"/>.
    /// </summary>
    [JsonPropertyName("openAiBaseUrl")]
    public string OpenAiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Which wire protocol the chat endpoint speaks: <c>OpenAI</c> or <c>Bedrock</c>.
    ///
    /// <b>Not a preference — a fact about the endpoint</b>, and the two are not interchangeable in
    /// either direction. An OpenAI-compatible gateway in front of Bedrock may list every Claude
    /// model and serve none of them over <c>/v1/chat/completions</c>; Bedrock's own runtime speaks
    /// <c>Converse</c> and answers an unknown path with HTTP 200. Choosing <c>Bedrock</c> also
    /// buys the only prompt caching this pipeline can ask for — see
    /// <see cref="BedrockChatClient"/>.
    ///
    /// Model ids are not portable between the two: a gateway calls it
    /// <c>anthropic.claude-haiku-4-5</c> and Bedrock wants
    /// <c>eu.anthropic.claude-haiku-4-5-20251001-v1:0</c>.
    /// </summary>
    [JsonPropertyName("chatProvider")]
    public string ChatProvider { get; set; } = EmbeddingProviders.OpenAi;

    [JsonPropertyName("availableModels")]
    public string AvailableModels { get; set; } = "gpt-4.1,gpt-4.1-mini,gpt-4o,gpt-4o-mini";

    [JsonPropertyName("selectedModel")]
    public string SelectedModel { get; set; } = "gpt-4.1";

    /// <summary>The model used to embed document chunks and search text.</summary>
    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Where embeddings are generated, when that isn't the same service as the chat model.
    /// Anthropic serves no embeddings endpoint, for instance, so a setup that runs checks on
    /// Claude has to embed somewhere else — OpenAI, Voyage, or a local Ollama.
    /// Leave empty to use <see cref="OpenAiBaseUrl"/>.
    /// </summary>
    [JsonPropertyName("embeddingBaseUrl")]
    public string EmbeddingBaseUrl { get; set; } = string.Empty;

    /// <summary>Key for the embedding service. Leave empty to use <see cref="OpenAiApiKey"/>.</summary>
    [JsonPropertyName("embeddingApiKey")]
    public string EmbeddingApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Which wire protocol the embedding endpoint speaks: <c>OpenAI</c> or <c>Bedrock</c>.
    ///
    /// <b>This is not a preference, it is a fact about the endpoint</b>, and getting it wrong
    /// fails in a way that took a session to diagnose. AWS Bedrock's runtime is not
    /// OpenAI-compatible — it takes <c>POST /model/{id}/invoke</c> with the model's own request
    /// shape, not <c>POST /embeddings</c> with OpenAI's. Worse, Bedrock answers an unrecognised
    /// path with <b>HTTP 200</b> and a Coral <c>UnknownOperationException</c> body, which the
    /// OpenAI client parses as a perfectly valid response containing zero embeddings. The error
    /// that reaches the user is "Expected the number of embeddings (0) to match the number of
    /// inputs (1)" — which names neither the endpoint, the protocol, nor the mistake.
    ///
    /// OpenAI-compatible gateways that front Bedrock for <i>chat</i> commonly serve no embedding
    /// models at all, so "the chat endpoint works" is not evidence that embeddings will.
    /// </summary>
    [JsonPropertyName("embeddingProvider")]
    public string EmbeddingProvider { get; set; } = EmbeddingProviders.OpenAi;

    /// <summary>
    /// Vector width of <see cref="EmbeddingModel"/>. It defines the Qdrant collection, so
    /// changing it means the collection has to be recreated.
    /// </summary>
    [JsonPropertyName("embeddingDimensions")]
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>
    /// Keep every table whole when chunking, as its own passage carrying the heading above it.
    ///
    /// <b>A table cut in half is worse than a table missing.</b> The semantic chunker splits on
    /// similarity and a token budget and knows nothing about tables, so a charges table or a fund
    /// allocation is cut wherever the budget runs out — delivering a header row with no figures, or
    /// figures with no header row. Neither answers the question that was asked of it, and the
    /// findings that turn on reading one row against its column heading are the ones the benchmark
    /// counts.
    ///
    /// <b>Changing this changes the index</b>, so a case has to be re-indexed for it to take
    /// effect. It is fingerprinted for the same reason: two runs chunked differently are not the
    /// same run, however identical everything downstream looks.
    /// </summary>
    [JsonPropertyName("tableAwareChunking")]
    public bool TableAwareChunking { get; set; } = true;

    /// <summary>
    /// Index and search a sparse lexical vector alongside the dense one, fusing the two with
    /// reciprocal rank fusion.
    ///
    /// <b>A routing fix, not a recall fix</b>, and the distinction is the whole justification.
    /// Retrieval already finds the passages — its recall is ~99% and it is 0.2% of run cost. What
    /// fails is that a passage reaches the wrong group: <c>33.4</c> reached exactly one pack while
    /// two checks needed it, and no query in either retrieves it. Dense embeddings are weakest on
    /// exactly the tokens those findings turn on — <c>48.06</c>, <c>£9,839.36</c>, <c>17.68</c>
    /// against <c>13.21</c> — because a general-purpose model puts two different four-figure sums
    /// in nearly the same place. Sparse matching is strongest there.
    ///
    /// <b>Turning this on or off changes the collection's shape</b>: a hybrid collection names its
    /// vectors and a dense-only one does not, and a point written for one cannot be read by the
    /// other. The collection has to be dropped and the case re-indexed either way — it cannot be
    /// migrated in place, and the store says so rather than failing obscurely.
    ///
    /// Off by default: it costs a re-index, and a run that has not had one should keep working.
    /// </summary>
    [JsonPropertyName("hybridRetrieval")]
    public bool HybridRetrieval { get; set; }

    [JsonPropertyName("documentFolder")]
    public string DocumentFolder { get; set; } = string.Empty;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonPropertyName("lastChecksCsvPath")]
    public string LastChecksCsvPath { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the Docling sidecar that converts spreadsheets to Markdown, e.g.
    /// http://localhost:5001. Leave empty to use <see cref="DefaultDoclingEndpoint"/>.
    /// </summary>
    [JsonPropertyName("doclingEndpoint")]
    public string DoclingEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Read pictures with a vision model during conversion.
    ///
    /// <b>Off by default, and the default is the important part.</b> It is a model call per picture
    /// per document, on a pipeline whose stated objective is minimum cost — and most pictures in a
    /// case bundle are letterhead, signatures and decorative rules. Switching it on is a decision
    /// about a specific case, not a setting to leave on.
    ///
    /// What it is <em>for</em> is the case where a table was pasted into a document as a bitmap.
    /// Docling can extract structure from a table and cannot extract structure from a picture of
    /// one, and OCR does not reach it — so where a document's tables are images, this is the only
    /// route to their content.
    ///
    /// <b>Changing it changes what conversion produces</b>, so a case has to be re-converted and
    /// re-indexed for it to take effect. It is fingerprinted for the same reason.
    /// </summary>
    [JsonPropertyName("pictureNarration")]
    public bool PictureNarration { get; set; }

    /// <summary>
    /// Re-express each converted table as prose with a model, alongside the table itself.
    ///
    /// <b>Off by default, and this default matters more than the other one.</b> The pipeline's
    /// design requires that tables survive as tables — the chunker keeps a table whole and the
    /// extractor reads the grid — so narration is an override for documents whose tables are
    /// genuinely prose-shaped, not the normal path. The narrative is always appended and never
    /// substituted: the grid stays exactly as it was.
    ///
    /// Fingerprinted, and requires a re-convert and a re-index, for the same reason as
    /// <see cref="PictureNarration"/>.
    /// </summary>
    [JsonPropertyName("tableNarration")]
    public bool TableNarration { get; set; }

    /// <summary>
    /// Which model narrates tables and reads pictures. Empty uses <see cref="SelectedModel"/>.
    ///
    /// Separate because the two jobs have different requirements from assessment: reading a picture
    /// needs a vision-capable model, and transcription is a cheaper task than adjudication, so the
    /// run that wants a strong assessor rarely wants to pay it to read letterheads.
    /// </summary>
    [JsonPropertyName("narrationModel")]
    public string NarrationModel { get; set; } = string.Empty;

    /// <summary>
    /// Smallest picture worth a model call, in bytes.
    ///
    /// Signature logos, icons and decorative rules arrive on nearly every document, and a vision
    /// call apiece costs real money to be told it is a logo. The reference measurement across three
    /// cases put the pictures carrying content between roughly 19 KB and 75 KB and the decoration
    /// near 2 KB, so the default sits deliberately below the content and above the noise.
    /// </summary>
    [JsonPropertyName("minimumImageBytes")]
    public int MinimumImageBytes { get; set; } = 4096;

    /// <summary>Most pictures read in one document, so a slide deck cannot run away with a run.</summary>
    [JsonPropertyName("maxImagesPerDocument")]
    public int MaxImagesPerDocument { get; set; } = 40;

    /// <summary>
    /// Base URL of the Qdrant instance holding the indexed chunks — the gRPC port (6334),
    /// not the REST port (6333). Leave empty to use <see cref="DefaultQdrantEndpoint"/>.
    /// </summary>
    [JsonPropertyName("qdrantEndpoint")]
    public string QdrantEndpoint { get; set; } = string.Empty;

    /// <summary>API key for a secured Qdrant. Empty for a local container.</summary>
    [JsonPropertyName("qdrantApiKey")]
    public string QdrantApiKey { get; set; } = string.Empty;

    [JsonPropertyName("qdrantCollection")]
    public string QdrantCollection { get; set; } = DefaultQdrantCollection;

    /// <summary>
    /// The tenant every indexed chunk is stamped with and every search is filtered by.
    /// One instance serves one tenant at a time.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public int TenantId { get; set; } = 99;

    /// <summary>
    /// The case reference stamped on every chunk and used to scope every search. Leave empty
    /// to derive it from the case folder's name, which is right when the folder is named
    /// after the case and wrong when it isn't — a working copy called "case-files" would
    /// otherwise index and search under that name.
    /// </summary>
    [JsonPropertyName("caseReference")]
    public string CaseReference { get; set; } = string.Empty;

    /// <summary>Upper bound on a chunk; the semantic chunker splits earlier when the topic shifts.</summary>
    [JsonPropertyName("maxTokensPerChunk")]
    public int MaxTokensPerChunk { get; set; } = 600;

    /// <summary>How much of the previous chunk is repeated, so a split mid-sentence still retrieves.</summary>
    [JsonPropertyName("chunkOverlapTokens")]
    public int ChunkOverlapTokens { get; set; } = 100;

    /// <summary>
    /// How many chunks one search-tool call may return to the model.
    ///
    /// <b>0 means unbounded</b> — every match the store holds for the query. See
    /// <see cref="IsUnbounded"/> for what that costs and why it is rarely the right answer:
    /// once groups sit at <see cref="MaxPassagesPerGroup"/>, extra candidates are retrieved and
    /// then discarded. Measured at 16, twice the retrieval bought two extra passages and
    /// category diversity fell.
    /// </summary>
    [JsonPropertyName("maxSearchResults")]
    public int MaxSearchResults { get; set; } = 8;

    /// <summary>
    /// How many passages of one group's retrieval are shown to the assessor. <b>0 means
    /// unbounded</b> — every passage the group's searches returned, after de-duplication.
    ///
    /// <b>This is the binding constraint on coverage, and it was a compile-time constant of 12
    /// until it became a setting.</b> Raising the search limit from 8 to 16 was measured and
    /// bought two extra passages across the whole run, because every group was already at the
    /// cap: 86% of hits were discarded and category diversity *fell*, since a bigger candidate
    /// set means the top twelve come from whichever documents score best. The cap, not the
    /// candidate set, is what decides what an assessor sees.
    ///
    /// The default is 24 rather than 12 because reserved slots ration the pack before ranking
    /// begins. The widest group declares four evidence categories and two evidence sections, so
    /// six of its twelve slots are spoken for, and the retrieval work that needs this headroom
    /// adds a seventh. At 24 a seven-query group drawing up to 112 candidates still discards
    /// 79% of them.
    ///
    /// <b>24 is reasoned, not measured.</b> No run on record has varied it. Treat the first run
    /// at this value as an experiment with its own measurement, and compare it against 12 before
    /// treating any recall change as attributable to anything else.
    /// </summary>
    [JsonPropertyName("maxPassagesPerGroup")]
    public int MaxPassagesPerGroup { get; set; } = 24;

    /// <summary>
    /// Print the whole assertion side of the case to every group, one line per populated
    /// canonical path, alongside the fragments the group's own plan selected.
    ///
    /// <b>The experiment this setting exists to run.</b> The claim and the fact that contradicts
    /// it routinely land in different prompts — traced end to end on the most severe finding of
    /// two separate cases — and the decision that put them there is a hand-authored list of paths
    /// in a plan file, invariant to model choice and wrong for about a fifth of the model. This is
    /// the cheapest test of that diagnosis: no new stage, no new vendor, one flag.
    ///
    /// <b>Turn it on together with a smaller pack.</b> Adding context here has been measured once
    /// and it went badly — 13% more prompt cost eleven points of recall — so the digest is meant
    /// to be paid for out of <see cref="MaxPassagesPerGroup"/> rather than added to it. The
    /// designed configuration is a pack of 12 with the digest on, which is a smaller prompt than a
    /// pack of 24 with it off. If recall falls at constant prompt size, the diagnosis is wrong and
    /// the moves that follow from it should not be built.
    ///
    /// Off by default: it changes every prompt in a run, so it is something a run opts into and
    /// records in its fingerprint, not something a fresh install does silently.
    /// </summary>
    [JsonPropertyName("assertionDigest")]
    public bool AssertionDigest { get; set; }

    /// <summary>
    /// The character budget for that digest, applied per group by dropping whole lines from the
    /// end. 0 turns it off as surely as the flag does.
    ///
    /// 40,000 is roughly 10k tokens, which on the measured models is about four hundred lines —
    /// enough for every populated path of both cases measured. It is a budget rather than a target:
    /// a digest that fits is printed whole, and one that does not says so in the prompt rather
    /// than trailing off.
    /// </summary>
    [JsonPropertyName("assertionDigestMaxChars")]
    public int AssertionDigestMaxChars { get; set; } = 40_000;

    /// <summary>
    /// How many assertions from elsewhere in the report a group may be shown because one of its
    /// own passages carries the same figure. <b>0 turns the join off.</b>
    ///
    /// The claim and the fact that contradicts it routinely land in different prompts, because
    /// which may meet which is authored by hand in the plans. This matches them in code instead —
    /// see <see cref="EvidenceJoin"/> — and the cap is here rather than in the code because it is
    /// a budget, and every budget in this prompt trades against the pack.
    ///
    /// Kept small deliberately. The mechanism this follows was measured broadcasting one item into
    /// 26 of 88 groups, 92 times over; a prompt that flags everything flags nothing.
    /// </summary>
    [JsonPropertyName("maxJoinedAssertions")]
    public int MaxJoinedAssertions { get; set; } = EvidenceJoin.MaxPerGroup;

    /// <summary>
    /// How many slots the pack holds for each category a group declared its evidence lives in,
    /// before the rest of the pack is filled by score.
    ///
    /// A floor, not a cap, so 0 means no reservation rather than unbounded. It exists because
    /// ranking by score alone silently lost whole documents: the Fact Find's prose scores below
    /// research and report prose for almost any query, and three checks reached it in zero
    /// groups out of nineteen while every pack sat exactly at the cap.
    /// </summary>
    [JsonPropertyName("reservedSlotsPerTargetedCategory")]
    public int ReservedSlotsPerTargetedCategory { get; set; } = 1;

    /// <summary>
    /// How many slots the pack holds for each evidence section a group named, within the
    /// categories it targeted. A floor, like
    /// <see cref="ReservedSlotsPerTargetedCategory"/>, so 0 means no reservation.
    ///
    /// A plan naming "Current Monthly Cash Flow" has asked for something more specific than one
    /// naming category B, and the section slot usually satisfies the category slot as a side
    /// effect. Three such hints moved four benchmark findings from missed to caught.
    /// </summary>
    [JsonPropertyName("reservedSlotsPerDeclaredSection")]
    public int ReservedSlotsPerDeclaredSection { get; set; } = 1;

    /// <summary>
    /// How many of a group's best-scoring passages are held before any other reservation runs.
    /// A floor like the two above, so 0 means no reservation.
    ///
    /// <b>Nothing used to reserve a slot for the best answer a group's own searches returned.</b>
    /// The section and category floors are served first, then the remainder is filled by score —
    /// and where the floors plus a cluster of near-identical chunks fill the cap, the top-scoring
    /// passage is evicted before the assessor sees it. Measured, twice, in two runs whose
    /// retrieval was byte-identical: a passage at 0.737 was dropped while eight near-duplicates
    /// at 0.627, all returned by one query, were kept.
    ///
    /// Score is the one signal that is neither the plan's guess nor the ranking's tie-break: it
    /// is what the group asked for, answered. Reserving one slot for it costs one slot of
    /// twenty-four.
    ///
    /// Blank forms and untargeted categories are excluded from the reservation — a passage that
    /// asserts nothing does not become the group's best answer by embedding well. See
    /// <c>CheckPlanRunner.Rank</c>.
    /// </summary>
    [JsonPropertyName("reservedSlotsForTopScore")]
    public int ReservedSlotsForTopScore { get; set; } = 1;

    /// <summary>
    /// How much of two passages from the same document must overlap before the lower-scoring one
    /// is dropped as a near-duplicate. <b>1.0 or more disables the pass entirely</b>, leaving only
    /// the exact-text de-duplication that has always run.
    ///
    /// Exact de-duplication catches a chunk returned verbatim by two wordings of the same
    /// question. It does not catch what overlapping chunk windows do to a long table: eight
    /// passages over the same rows, 95% the same text, eight distinct keys, and a third of the
    /// cap gone to one query's view of one table. That is not evidence, it is the same evidence
    /// eight times, and the slots it takes come from passages the group has no other route to.
    ///
    /// 0.9 is deliberately conservative — two chunks must be near-identical, not merely similar,
    /// because a table's rows genuinely repeat their column labels and two different rows of the
    /// same table are two different facts.
    /// </summary>
    [JsonPropertyName("nearDuplicateOverlap")]
    public double NearDuplicateOverlap { get; set; } = 0.9;

    /// <summary>
    /// How much of the extraction's self-report reaches an assessor. <b>0 means unbounded.</b>
    ///
    /// The report is what lets a group tell a suitability report that is genuinely silent from
    /// one the extraction failed to read, and it carries the contradictions extraction found in
    /// the report itself — the only route CHK-001's internal-consistency requirement has to
    /// them. Sized to hold a whole report: the observed model is around 9,000 characters and a
    /// previous 4,000-character cap dropped 55% of it.
    /// </summary>
    [JsonPropertyName("extractionReportMaxChars")]
    public int ExtractionReportMaxChars { get; set; } = 24_000;

    /// <summary>
    /// Output cap for one group's decision. <b>0 means unbounded</b> — the provider's own
    /// maximum for the model.
    ///
    /// This caps how much the assessor can write, so it caps how many findings one group may
    /// report. Distinct from <see cref="MaxTokens"/>, which bounds ordinary chat replies.
    /// </summary>
    [JsonPropertyName("decisionMaxTokens")]
    public int DecisionMaxTokens { get; set; } = 8000;

    /// <summary>
    /// Run only the queries the plans mark <c>Core</c>, skipping <c>Supplementary</c> ones.
    ///
    /// Roughly one query in seven is Supplementary across the ten plans, so this is the
    /// cheapest retrieval saving available — and the plan authors chose which queries are
    /// expendable, rather than a score threshold choosing for them. Off by default: the
    /// full set is what the coverage matrix describes.
    /// </summary>
    [JsonPropertyName("coreQueriesOnly")]
    public bool CoreQueriesOnly { get; set; }

    /// <summary>
    /// Run every check's full evidence-gathering and assessment, even when its trigger probe
    /// says the check does not apply and would otherwise settle as Not Applicable.
    ///
    /// Trigger probes exist to save the cost of assessing checks the case plainly doesn't
    /// need. Turn this on to audit that judgement itself — confirming a trigger's N/A verdict
    /// was correct, or debugging why a trigger looks wrong — since a suppressed check never
    /// reaches the assessor and so leaves no evidence trail to inspect. Off by default: normal
    /// runs should keep the cost saving.
    /// </summary>
    [JsonPropertyName("ignoreTriggerProbe")]
    public bool IgnoreTriggerProbe { get; set; }

    /// <summary>
    /// How many requests the whole run may have in flight at once.
    ///
    /// A run-wide budget rather than a per-check one. Checks are assessed in parallel and so are
    /// the requirements within them, and bounding each level separately multiplies: ten checks
    /// four at a time, each assessing four requirements at a time, is sixteen requests from two
    /// settings that both read "4". A provider's rate limit applies to the total, so this does
    /// too — and a check with three requirements no longer leaves the budget idle, because
    /// another check's requirements take the free slots.
    ///
    /// Six is a deliberate default: enough to keep a run moving, low enough that most gateways
    /// will not rate-limit it. Raise it if yours tolerates more — this is the dial that decides
    /// how long a run takes. Results are collected by position, so it changes the duration of a
    /// run and never its conclusions.
    /// </summary>
    [JsonPropertyName("maxParallelRequests")]
    public int MaxParallelRequests { get; set; } = 6;

    /// <summary>
    /// How many checks may be in progress at once.
    ///
    /// This is not a second concurrency budget — <see cref="MaxParallelRequests"/> is the only
    /// thing bounding actual requests. It bounds how many checks are part-finished at any moment,
    /// which is a readability question rather than a throughput one: a screen showing ten checks
    /// all half-done tells you less than one showing four progressing and six waiting.
    /// </summary>
    [JsonPropertyName("maxParallelChecks")]
    public int MaxParallelChecks { get; set; } = 4;

    /// <summary>
    /// The largest piece of text handed to the embedding endpoint in one call.
    ///
    /// This is not about chunk size — chunks are bounded by <see cref="MaxTokensPerChunk"/> and
    /// are never close to it. It is about the semantic chunker, which embeds the document's own
    /// elements to decide where to cut, and will therefore hand over whatever the Markdown
    /// reader produced. A converted policy document that the reader collapses into a single
    /// element arrives as one 150,000-character call, and the provider rejects it — losing the
    /// entire document rather than one passage of it.
    ///
    /// The default clears every mainstream provider's limit with room to spare: Amazon Titan
    /// caps an input at 50,000 characters, and OpenAI's 8,192-token cap works out around 32,000.
    /// Lower it for an endpoint stricter than either.
    /// </summary>
    [JsonPropertyName("maxEmbeddingInputCharacters")]
    public int MaxEmbeddingInputCharacters { get; set; } = 20_000;

    /// <summary>
    /// The canonical model JSON Schema the suitability report is extracted into. Leave empty
    /// to look for <see cref="DefaultCanonicalSchemaFileName"/> beside the executable.
    /// </summary>
    [JsonPropertyName("canonicalSchemaPath")]
    public string CanonicalSchemaPath { get; set; } = string.Empty;

    /// <summary>
    /// Folder holding the per-check query plans (CHK-*.query-plan.json). Leave empty to look
    /// for a "check-plan" folder beside the executable.
    /// </summary>
    [JsonPropertyName("checkPlanFolder")]
    public string CheckPlanFolder { get; set; } = string.Empty;

    /// <summary>
    /// Where extracted canonical models are kept. Leave empty for a file in the app's local
    /// data folder, so models survive a restart without the user choosing a location.
    /// </summary>
    [JsonPropertyName("canonicalModelDbPath")]
    public string CanonicalModelDbPath { get; set; } = string.Empty;

    /// <summary>
    /// Output cap for one extraction pass. Extraction returns a JSON section rather than
    /// prose, and a truncated section is unusable, so it needs far more headroom than a
    /// check response — the app extracts a section at a time to stay inside this.
    ///
    /// Raised from 16,000 after a run lost its recommendations section to this cap: the
    /// largest section that succeeded used about 14,100 tokens and recommendations needed
    /// roughly 17,500, so the old default sat between the two. A truncated pass is now
    /// salvaged and reported rather than silently retried, but headroom is the cheaper fix.
    /// </summary>
    [JsonPropertyName("extractionMaxTokens")]
    public int ExtractionMaxTokens { get; set; } = 32000;

    /// <summary>
    /// Pins temperature so the same evidence pack produces the same finding — the provider
    /// default is a creative temperature, which is the single largest source of run-to-run
    /// disagreement. Turn it off for a model that rejects a fixed temperature; some do (a
    /// Bedrock inference profile for Claude Sonnet 5 accepts only temperature 1, for instance).
    /// </summary>
    [JsonPropertyName("pinTemperature")]
    public bool PinTemperature { get; set; } = true;

    /// <summary>Temperature sent with every call when <see cref="PinTemperature"/> is on. 0 is fully deterministic.</summary>
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0f;

    /// <summary>Pins top-p (nucleus sampling) the same way <see cref="PinTemperature"/> pins temperature.</summary>
    [JsonPropertyName("pinTopP")]
    public bool PinTopP { get; set; } = true;

    /// <summary>Top-p sent with every call when <see cref="PinTopP"/> is on.</summary>
    [JsonPropertyName("topP")]
    public float TopP { get; set; } = 1f;

    /// <summary>
    /// Pins the seed the same way <see cref="PinTemperature"/> pins temperature. Turn it off
    /// for a gateway that rejects the parameter outright; Bedrock's Anthropic route does.
    /// </summary>
    [JsonPropertyName("pinSeed")]
    public bool PinSeed { get; set; } = true;

    /// <summary>
    /// The seed sent with every call when <see cref="PinSeed"/> is on. Its value does not
    /// matter; holding it constant does. Change it deliberately to sample a second opinion on
    /// a check that keeps flipping.
    /// </summary>
    [JsonPropertyName("samplingSeed")]
    public long SamplingSeed { get; set; } = 1;

    /// <summary>
    /// Constrains a check finding to the schema the app parses, rather than asking for JSON
    /// and hoping. It removes the whole class of variation where a rewording of the outcome
    /// changes the outcome — an unrecognised value is read as a concern, so a wobble in the
    /// envelope becomes a wobble in the result.
    ///
    /// Requires an endpoint that supports JSON-schema response formats. Turn it off for one
    /// that does not.
    /// </summary>
    [JsonPropertyName("structuredFindings")]
    public bool StructuredFindings { get; set; } = true;

    /// <summary>
    /// Varies every prompt this run sends, so the gateway cannot answer it from cache.
    ///
    /// <b>Off by default, because the cache is usually doing something useful.</b> The prefix
    /// cache over the check header is what makes one call per requirement affordable, and
    /// paying the full input rate on every group to prove a point is a real cost.
    ///
    /// It exists because a cached reply is indistinguishable from a generated one, and that has
    /// twice cost a measurement. A run of the same case came back in 4.4 seconds carrying
    /// 149,582 output tokens — 33,858 tok/s, which no model produces — and two whole analyses
    /// were written before <see cref="RunAuthenticity"/> existed to say so. An extraction
    /// re-issued with byte-identical requests returned four sections differing by up to 8,684
    /// characters, so a replay is not even a stable replay. Neither the model digest nor the
    /// fingerprint can see any of this: the inputs were identical, so they report identical.
    ///
    /// Turn it on when the answer has to be freshly generated — measuring determinism, scoring
    /// a configuration change, or re-running an extraction whose trigger flags look wrong.
    /// <see cref="PromptCacheBypass"/> is what actually varies the prompt.
    /// </summary>
    [JsonPropertyName("bypassResponseCache")]
    public bool BypassResponseCache { get; set; }

    /// <summary>
    /// Where each run's evidence, prompts and findings are kept, so a finished run can be
    /// reopened as a report rather than only read back as a log.
    ///
    /// Its own file rather than a table in the canonical-model database, for the same reason
    /// that one is separate from the vector store: they have different lifetimes. A run archive
    /// grows with every assessment and is expected to be pruned; a canonical model is one row
    /// per case that cost real tokens to produce and must not go with a clear-out of run
    /// history. Leave empty for a file beside the canonical models.
    /// </summary>
    [JsonPropertyName("checkRunDbPath")]
    public string CheckRunDbPath { get; set; } = string.Empty;

    /// <summary>The file name looked for beside the executable when no schema path is set.</summary>
    public const string DefaultCanonicalSchemaFileName = "canonical-suitability-model.schema.json";

    /// <summary>The folder name looked for beside the executable when no plan folder is set.</summary>
    public const string DefaultCheckPlanFolderName = "check-plan";

    /// <summary>Where prompts sent to the LLM are logged by default, one file per run.</summary>
    public const string DefaultPromptLogFolder = @"C:\logs\apipromptevaluator";

    /// <summary>
    /// Folder every run's prompt log is written to. Leave empty to use
    /// <see cref="DefaultPromptLogFolder"/>.
    /// </summary>
    [JsonPropertyName("promptLogFolder")]
    public string PromptLogFolder { get; set; } = string.Empty;

    /// <summary>
    /// The configured schema path, or the copy deployed beside the executable. Returns the
    /// resolved path whether or not it exists — the caller reports a missing file with the
    /// path it looked for, which is more useful than an empty string.
    /// </summary>
    public string ResolveCanonicalSchemaPath() =>
        string.IsNullOrWhiteSpace(CanonicalSchemaPath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultCanonicalSchemaFileName)
            : CanonicalSchemaPath.Trim();

    /// <summary>The configured plan folder, or the one deployed beside the executable.</summary>
    public string ResolveCheckPlanFolder() =>
        string.IsNullOrWhiteSpace(CheckPlanFolder)
            ? Path.Combine(AppContext.BaseDirectory, DefaultCheckPlanFolderName)
            : CheckPlanFolder.Trim();

    /// <summary>The configured prompt log folder, or <see cref="DefaultPromptLogFolder"/>.</summary>
    public string ResolvePromptLogFolder() =>
        string.IsNullOrWhiteSpace(PromptLogFolder)
            ? DefaultPromptLogFolder
            : PromptLogFolder.Trim();

    /// <summary>
    /// Where the canonical model database lives. The default sits next to settings.json so
    /// an extracted model is found again after a restart without any configuration.
    /// </summary>
    public string ResolveCanonicalModelDbPath() =>
        string.IsNullOrWhiteSpace(CanonicalModelDbPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AiPromptEvaluator",
                "canonical-models.db")
            : CanonicalModelDbPath.Trim();

    /// <summary>
    /// Where the run archive lives. Defaults to a file beside the canonical models, so the two
    /// halves of a case's history sit together without either being configured.
    /// </summary>
    public string ResolveCheckRunDbPath() =>
        string.IsNullOrWhiteSpace(CheckRunDbPath)
            ? Path.Combine(
                Path.GetDirectoryName(ResolveCanonicalModelDbPath()) ?? AppContext.BaseDirectory,
                "check-runs.db")
            : CheckRunDbPath.Trim();

    /// <summary>The configured Docling endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveDoclingEndpoint() =>
        Resolve(DoclingEndpoint, DefaultDoclingEndpoint);

    /// <summary>The configured Qdrant endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveQdrantEndpoint() =>
        Resolve(QdrantEndpoint, DefaultQdrantEndpoint);

    /// <summary>The configured OpenAI-compatible base URL, or the default.</summary>
    public string ResolveBaseUrl() =>
        Resolve(OpenAiBaseUrl, DefaultBaseUrl);

    /// <summary>
    /// Resolves a coverage cap that uses <c>0</c> to mean unbounded.
    ///
    /// <b>Zero, not -1.</b> Every cap this applies to is a count of things shown to a model, and
    /// zero of them is never a configuration anyone wants — a run with no passages, no findings
    /// or no extraction report is not a cheaper run, it is a broken one. So the value that would
    /// otherwise be a footgun is spent on the meaning that has no other spelling, and a negative
    /// value from a hand-edited settings file reads as unbounded too rather than throwing at the
    /// far end of a long run.
    ///
    /// Floors are the exception and do not go through here: a reserved-slot count of 0 means no
    /// reservation, which is a real and useful setting.
    /// </summary>
    public static int Unbounded(int cap) => cap <= 0 ? int.MaxValue : cap;

    /// <summary>Whether a cap has been set to unbounded, for the run fingerprint and the logs.</summary>
    public static bool IsUnbounded(int cap) => cap <= 0;

    /// <summary>Where embeddings are generated — its own endpoint when set, else the chat one.</summary>
    public string ResolveEmbeddingBaseUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingBaseUrl) ? ResolveBaseUrl() : Resolve(EmbeddingBaseUrl, DefaultBaseUrl);

    /// <summary>The embedding service's key, falling back to the chat key when they're the same service.</summary>
    public string ResolveEmbeddingApiKey() =>
        string.IsNullOrWhiteSpace(EmbeddingApiKey) ? OpenAiApiKey.Trim() : EmbeddingApiKey.Trim();

    /// <summary>
    /// True when embeddings are pointed at a service that has no embeddings endpoint. Worth
    /// saying plainly: no base path or model name makes that call work.
    /// </summary>
    public bool EmbeddingEndpointCannotEmbed() =>
        ResolveEmbeddingBaseUrl().Contains("api.anthropic.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The configured case reference, or the case folder's name when none is set. This is the
    /// single answer to "which case is this", so indexing and searching can't disagree.
    /// </summary>
    public string ResolveCaseReference(string caseFolder) =>
        string.IsNullOrWhiteSpace(CaseReference)
            ? CaseDocumentIndexer.CaseReferenceFor(caseFolder)
            : CaseReference.Trim();

    public string ResolveCollection() =>
        string.IsNullOrWhiteSpace(QdrantCollection) ? DefaultQdrantCollection : QdrantCollection.Trim();

    private static string Resolve(string configured, string fallback) =>
        (string.IsNullOrWhiteSpace(configured) ? fallback : configured).Trim().TrimEnd('/');

    public List<string> ParseModels() =>
        AvailableModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
