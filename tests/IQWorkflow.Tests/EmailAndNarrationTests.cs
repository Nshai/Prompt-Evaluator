using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

using AiPromptEvaluator;

using IQWorkflow;

using MimeKit;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The three ingestion fixes that need something beyond the file itself: email, picture
/// transcription and table narration.
///
/// <b>The two narration passes are the only part of conversion that spends money</b>, so most of
/// what is proved here is that they do not run. A pass that quietly switched itself on would show up
/// as a bill nobody could attribute, on a pipeline whose stated objective is minimum cost.
/// </summary>
public sealed class EmailAndNarrationTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-email", Guid.NewGuid().ToString("N"));

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

    // ──────────────────────────────────────────────
    // The configuration defaults
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Both narration passes are off out of the box.</b> Picture transcription is a vision call
    /// per picture and most pictures in a case bundle are letterhead; table narration re-expresses a
    /// grid the pipeline already reads perfectly well. Either left on by default would spend money
    /// on every run for a benefit that only some documents have.
    /// </summary>
    [Fact]
    public void NarrationIsOffUnlessItIsSwitchedOn()
    {
        var settings = new AppSettings();

        Assert.False(settings.PictureNarration);
        Assert.False(settings.TableNarration);
    }

    /// <summary>
    /// Switching either on changes what conversion produces, so two runs that differ by one is not
    /// the same run — and the fingerprint has to say so, or a comparison between them is unfounded.
    /// </summary>
    [Fact]
    public void SwitchingNarrationOnChangesTheRunFingerprint()
    {
        var plain = new AppSettings();
        var narrating = new AppSettings { TableNarration = true, PictureNarration = true };

        Assert.NotEqual(
            RunFingerprint.DigestOfSettings(plain),
            RunFingerprint.DigestOfSettings(narrating));

        var named = RunFingerprint.NonDefaultSettings(narrating);

        Assert.Contains(nameof(AppSettings.TableNarration), named);
        Assert.Contains(nameof(AppSettings.PictureNarration), named);
    }

    // ──────────────────────────────────────────────
    // Email: reading
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>The header block is the half that gets dropped, and Test Case 3 turns on it:</b> the
    /// recommendation meeting was scheduled for 30 March, six days after the suitability report is
    /// dated. A converter that keeps the prose and loses the Date header loses the finding.
    /// </summary>
    [Fact]
    public void TheHeaderBlockCarriesTheDateAndTheAttachmentNames()
    {
        var header = EmailDocumentConverter.BuildHeader(
            "Your pension review",
            "adviser@firm.example",
            "client@example.com",
            null,
            "2026-03-30 09:14:00",
            ["order.pdf", "forecast.jpg"]);

        Assert.Contains("Date: 2026-03-30 09:14:00", header);
        Assert.Contains("Attachments: order.pdf; forecast.jpg", header);
        Assert.DoesNotContain("Cc:", header);
    }

    /// <summary>A missing header reads as absent rather than as blank, so a quote from it is honest.</summary>
    [Fact]
    public void AMissingHeaderSaysSo() =>
        Assert.Contains("From: (not stated)", EmailDocumentConverter.BuildHeader(
            "Subject", null, "someone", null, "2026-01-01", []));

    /// <summary>
    /// Every tag becomes a space, so a figure that was emphasised is separated from the punctuation
    /// that followed it — "£116,997.47 ." rather than "£116,997.47.". That is the ported behaviour
    /// and it is the right trade: the alternative, joining across a tag boundary, would run two
    /// words together and produce a figure that appears in no source document.
    /// </summary>
    [Fact]
    public void AnHtmlOnlyBodyIsReducedToItsText() =>
        Assert.Equal(
            "The transfer value is £116,997.47 .",
            EmailDocumentConverter.StripHtml("<p>The transfer value is <b>£116,997.47</b>.</p>"));

    /// <summary>
    /// An inline image has no file name — it is referenced by Content-Id from the HTML body — so one
    /// is derived from its media subtype. Without an extension the converter would reject it as
    /// unconvertible, which is how the content would be lost a second time.
    /// </summary>
    [Fact]
    public void AnInlineImageWithNoFileNameGetsOneFromItsMediaType()
    {
        var part = new MimePart("image", "jpeg") { Content = new MimeContent(new MemoryStream([1])) };

        Assert.Equal("inline-3.jpg", EmailDocumentConverter.NameFor(part, 3));
    }

    /// <summary>
    /// The name comes from the message, which came from outside. A traversal in an attachment name
    /// is a well-worn trick and costs one line to refuse.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\startup\payload.exe", "payload.exe")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("normal.pdf", "normal.pdf")]
    public void AnAttachmentNameCannotEscapeItsFolder(string given, string expected) =>
        Assert.Equal(expected, EmailDocumentConverter.SafeFileName(given));

    /// <summary>
    /// Two messages in a thread both carrying <c>statement.pdf</c> must not write over each other,
    /// and a reader should be able to see which message delivered which.
    /// </summary>
    [Fact]
    public void AnAttachmentIsNamedForTheMessageThatCarriedIt() =>
        Assert.Equal(
            "covering-note-statement",
            EmailDocumentConverter.OutputNameFor("covering-note", "statement.pdf"));

    // ──────────────────────────────────────────────
    // Email: converting
    // ──────────────────────────────────────────────

    /// <summary>An inner converter that records what it was handed and answers with fixed Markdown.</summary>
    private sealed class RecordingConverter : ICaseDocumentConverter
    {
        public List<string> Converted { get; } = [];

        public Task<IReadOnlyList<DocumentConversion>> ConvertAsync(
            string path, string relativeSource, string categoryCode,
            CancellationToken cancellationToken = default)
        {
            Converted.Add(Path.GetFileName(path));

            return Task.FromResult<IReadOnlyList<DocumentConversion>>(
                [new DocumentConversion(relativeSource, categoryCode, "# Converted\n\nBody.")]);
        }
    }

    /// <summary>Writes a .eml with the parts a test asks for.</summary>
    private string WriteEmail(string name, params (string FileName, int Bytes)[] attachments)
    {
        var body = new Multipart("mixed") { new TextPart("plain") { Text = "Please find attached." } };

        foreach (var (fileName, bytes) in attachments)
        {
            body.Add(new MimePart("application", "pdf")
            {
                FileName = fileName,
                Content = new MimeContent(new MemoryStream(new byte[bytes])),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            });
        }

        var message = new MimeMessage
        {
            Subject = "Your pension review",
            Date = new DateTimeOffset(2026, 3, 30, 9, 14, 0, TimeSpan.Zero),
            Body = body,
        };

        message.From.Add(new MailboxAddress("Adviser", "adviser@firm.example"));
        message.To.Add(new MailboxAddress("Client", "client@example.com"));

        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, name);
        message.WriteTo(path);

        return path;
    }

    /// <summary>
    /// <b>The attachment is routinely the real evidence.</b> One .msg in Test Case 3 carries the
    /// certified Pension Sharing Order whose ticked "Internal transfer" box is the case's central
    /// contradiction. Reading the covering note and discarding what it delivered would be the most
    /// expensive kind of thorough.
    /// </summary>
    [Fact]
    public async Task AnEmailProducesTheNoteAndEveryAttachmentItCarried()
    {
        var inner = new RecordingConverter();
        var path = WriteEmail("note.eml", ("order.pdf", 8000));

        var bundle = await new EmailDocumentConverter(inner)
            .ConvertAsync(path, "Correspondence/note.eml", "Correspondence");

        Assert.Equal(2, bundle.Count);

        // The message itself, read here rather than by the sidecar — Docling reads neither format.
        Assert.Contains("Date: 2026-03-30", bundle[0].Markdown);
        Assert.Contains("Please find attached.", bundle[0].Markdown);
        Assert.Equal("note", bundle[0].OutputName);

        // The attachment, converted by the real converter and named for the message that delivered
        // it, with the provenance a claim about "when this arrived" has to be able to quote.
        Assert.Equal("note-order", bundle[1].OutputName);
        Assert.Contains("Attached to \"Your pension review\"", bundle[1].Markdown);
        Assert.Contains("# Converted", bundle[1].Markdown);
        Assert.Equal("Correspondence/note.eml › order.pdf", bundle[1].Source);
    }

    /// <summary>
    /// Signature images and tracking pixels arrive on every message in a thread. Converting them
    /// costs a round trip each and yields a logo.
    /// </summary>
    [Fact]
    public async Task ASignatureSizedAttachmentIsNotWorthARoundTrip()
    {
        var inner = new RecordingConverter();
        var path = WriteEmail("note.eml", ("logo.pdf", 200));

        var bundle = await new EmailDocumentConverter(inner)
            .ConvertAsync(path, "Correspondence/note.eml", "Correspondence");

        Assert.Single(bundle);
        Assert.Empty(inner.Converted);
    }

    /// <summary>Everything that is not an email goes straight through, untouched.</summary>
    [Fact]
    public async Task ANonEmailIsDelegated()
    {
        var inner = new RecordingConverter();
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, "report.pdf");

        await File.WriteAllTextAsync(path, "not really a pdf");

        var bundle = await new EmailDocumentConverter(inner)
            .ConvertAsync(path, "Suitability/report.pdf", "Suitability");

        Assert.Equal(["report.pdf"], inner.Converted);
        Assert.Null(Assert.Single(bundle).OutputName);
    }

    /// <summary>
    /// A message nobody can parse is one document's problem, recorded — not an exception that loses
    /// the case. The same reasoning as an encrypted PDF.
    /// </summary>
    [Fact]
    public async Task AnUnreadableMessageIsRecordedRatherThanThrown()
    {
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, "broken.msg");

        await File.WriteAllTextAsync(path, "this is not an Outlook message");

        var conversion = Assert.Single(await new EmailDocumentConverter(new RecordingConverter())
            .ConvertAsync(path, "Correspondence/broken.msg", "Correspondence"));

        Assert.False(conversion.Succeeded);
        Assert.Contains("could not be read", conversion.Error);
    }

    /// <summary>Email reaches the conversion stage at all: Docling reads neither format.</summary>
    [Theory]
    [InlineData("note.eml", true)]
    [InlineData("note.msg", true)]
    [InlineData("report.pdf", true)]
    [InlineData("already.md", false)]
    [InlineData("archive.zip", false)]
    public void EmailIsSomethingTheStageConverts(string name, bool expected) =>
        Assert.Equal(expected, ConvertDocumentsStage.IsSource(name));

    // ──────────────────────────────────────────────
    // Table narration
    // ──────────────────────────────────────────────

    private static TableNarrationRequest Request(params string[][] rows) => new(
        "charges.pdf", 0, ["Fund", "Charge"],
        [.. rows.Select(r => (IReadOnlyList<string>)r)], 1, 1, null);

    /// <summary>
    /// The deterministic narrator is not only a fallback — it is what makes the no-loss guarantee
    /// real. Every header/value pair is rendered explicitly, so the prose is mechanical and nothing
    /// is lost.
    /// </summary>
    [Fact]
    public async Task TheDeterministicNarratorNamesEveryColumnForEveryValue()
    {
        var narrative = await new RuleBasedTableNarrator()
            .NarrateAsync(Request(["Nest Higher Risk", "0.30%"]));

        Assert.Equal("- Fund is Nest Higher Risk; Charge is 0.30%.\n", narrative);
    }

    /// <summary>
    /// <b>This check is the only reason narration is safe to offer at all.</b> A model asked to
    /// write prose will occasionally summarise a row away, and a silently missing charge is exactly
    /// the class of loss this pipeline exists to catch rather than create.
    /// </summary>
    [Fact]
    public void AValueTheModelDroppedIsPutBackVerbatim()
    {
        var request = Request(["Nest Higher Risk", "0.30%"], ["Nest Sharia", "0.75%"]);

        var repaired = LlmTableNarrator.AppendMissingValues(
            "The Nest Higher Risk fund charges 0.30%.", request);

        Assert.Contains("Also noted: Nest Sharia; 0.75%.", repaired);
    }

    [Fact]
    public void ANarrativeThatKeptEveryValueIsLeftAlone()
    {
        const string complete = "Nest Higher Risk charges 0.30%.";

        Assert.Equal(complete, LlmTableNarrator.AppendMissingValues(
            complete, Request(["Nest Higher Risk", "0.30%"])));
    }

    [Fact]
    public void ThePromptCarriesTheHeadersTheRowsAndTheChunkPosition()
    {
        var prompt = LlmTableNarrator.BuildPrompt(new TableNarrationRequest(
            "charges.pdf", 2, ["Fund", "Charge"],
            [["Nest Higher Risk", "0.30%"]], 2, 3, "Charges"));

        Assert.Contains("Fund | Charge", prompt);
        Assert.Contains("Nest Higher Risk | 0.30%", prompt);
        Assert.Contains("part 2 of 3", prompt);
        Assert.Contains("Charges", prompt);
    }

    // ──────────────────────────────────────────────
    // Picture transcription
    // ──────────────────────────────────────────────

    [Fact]
    public void AnEmbeddedImageIsDecodedFromItsDataUri()
    {
        Assert.True(DataUri.TryParse(
            "data:image/jpeg;base64," + Convert.ToBase64String([1, 2, 3, 4]),
            out var bytes,
            out var mediaType));

        Assert.Equal(4, bytes.Length);
        Assert.Equal("image/jpeg", mediaType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/picture.png")]
    [InlineData("data:image/png,notbase64")]
    [InlineData("data:image/png;base64,!!!!")]
    public void AnythingThatIsNotAnEmbeddedImageIsRefused(string? uri) =>
        Assert.False(DataUri.TryParse(uri, out _, out _));

    // ──────────────────────────────────────────────
    // The converter, with the sidecar stubbed
    // ──────────────────────────────────────────────

    /// <summary>Answers every conversion with the canned docling-serve response a test supplies.</summary>
    private sealed class StubSidecar(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    /// <summary>A response carrying one picture and one two-row table.</summary>
    private static string SidecarResponse(int imageBytes)
    {
        var image = Convert.ToBase64String(new byte[imageBytes]);

        return $$"""
            {
              "status": "success",
              "document": {
                "filename": "report.pdf",
                "md_content": "# Report\n\nThe client is a tenant.\n",
                "json_content": {
                  "pictures": [ { "image": { "uri": "data:image/png;base64,{{image}}" } } ],
                  "tables": [ {
                    "data": {
                      "num_rows": 2, "num_cols": 2,
                      "grid": [
                        [ { "text": "Fund", "column_header": true },
                          { "text": "Charge", "column_header": true } ],
                        [ { "text": "Nest Higher Risk" }, { "text": "0.30%" } ]
                      ]
                    }
                  } ]
                }
              }
            }
            """;
    }

    private DoclingMarkdownConverter Converter(
        int imageBytes = 20_000,
        PictureNarrationOptions? pictures = null,
        IPictureDescriber? describer = null,
        TableNarrationOptions? tables = null,
        ITableNarrator? narrator = null) =>
        new(
            new DoclingClient(
                new DoclingOptions { BaseUrl = "http://stub" },
                new HttpClient(new StubSidecar(SidecarResponse(imageBytes)))),
            pictures, describer, tables, narrator);

    private string SourceFile()
    {
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, "report.pdf");
        File.WriteAllText(path, "source");

        return path;
    }

    private sealed class CountingDescriber(string? answer = "Risk level: 5 of 7.") : IPictureDescriber
    {
        public int Calls { get; private set; }

        public Task<string?> DescribeAsync(
            ReadOnlyMemory<byte> image, string mediaType, string? context, CancellationToken ct)
        {
            Calls++;

            return Task.FromResult(answer);
        }
    }

    /// <summary>
    /// <b>The default converts without asking a model anything.</b> This is the property the whole
    /// opt-in design exists for: a run that has not switched narration on makes no chat call during
    /// conversion, and therefore needs no chat credential to convert a case.
    /// </summary>
    [Fact]
    public async Task NothingIsSentToAModelUnlessNarrationIsSwitchedOn()
    {
        var describer = new CountingDescriber();
        var narrator = new RuleBasedTableNarrator();

        var conversion = await Converter(describer: describer, narrator: narrator)
            .ConvertOneAsync(SourceFile(), "A/report.pdf", "A");

        Assert.Equal(0, describer.Calls);
        Assert.Equal(0, conversion.TranscribedPictures);
        Assert.Equal(0, conversion.NarratedTables);
        Assert.DoesNotContain("transcribed", conversion.Markdown);
        Assert.DoesNotContain("Narrative reading", conversion.Markdown);
    }

    /// <summary>
    /// Where a document's tables are bitmaps this is the only route to their content — Docling
    /// cannot extract structure from a picture of a table, and OCR does not reach it.
    /// </summary>
    [Fact]
    public async Task ATranscribedPictureIsAppendedAndLabelledAsATranscription()
    {
        var describer = new CountingDescriber();

        var conversion = await Converter(
                pictures: new PictureNarrationOptions { Enabled = true }, describer: describer)
            .ConvertOneAsync(SourceFile(), "A/report.pdf", "A");

        Assert.Equal(1, describer.Calls);
        Assert.Equal(1, conversion.TranscribedPictures);
        Assert.Contains("Risk level: 5 of 7.", conversion.Markdown);

        // Labelled, because a reading of a table is not the table. A citation verifier checking a
        // quote against the source has to be able to tell which of the two it is holding.
        Assert.Contains("### Image 1 (transcribed)", conversion.Markdown);

        // And the document's own text is untouched.
        Assert.Contains("The client is a tenant.", conversion.Markdown);
    }

    /// <summary>
    /// A vision call apiece to be told something is a logo costs real money for nothing. The
    /// measured split put content between roughly 19 KB and 75 KB and decoration near 2 KB.
    /// </summary>
    [Fact]
    public async Task ADecorativeImageIsNotWorthAVisionCall()
    {
        var describer = new CountingDescriber();

        var conversion = await Converter(
                imageBytes: 900,
                pictures: new PictureNarrationOptions { Enabled = true, MinimumImageBytes = 4096 },
                describer: describer)
            .ConvertOneAsync(SourceFile(), "A/report.pdf", "A");

        Assert.Equal(0, describer.Calls);
        Assert.Equal(0, conversion.TranscribedPictures);
    }

    /// <summary>A picture carrying nothing readable adds nothing, rather than an empty section.</summary>
    [Fact]
    public async Task APictureWithNothingToReadAddsNothing()
    {
        var conversion = await Converter(
                pictures: new PictureNarrationOptions { Enabled = true },
                describer: new CountingDescriber(answer: null))
            .ConvertOneAsync(SourceFile(), "A/report.pdf", "A");

        Assert.Equal(0, conversion.TranscribedPictures);
        Assert.DoesNotContain("transcribed", conversion.Markdown);
    }

    /// <summary>
    /// <b>Appended, never substituted.</b> The pipeline's design requires that tables survive as
    /// tables — the chunker keeps a table whole and the extractor reads the grid — so the narrative
    /// is a second reading of the same rows and the document's own table is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task ANarratedTableIsAddedBesideTheTableRatherThanInsteadOfIt()
    {
        var conversion = await Converter(
                tables: new TableNarrationOptions { Enabled = true },
                narrator: new RuleBasedTableNarrator())
            .ConvertOneAsync(SourceFile(), "A/report.pdf", "A");

        Assert.Equal(1, conversion.NarratedTables);
        Assert.Contains("### Narrative reading of table 1", conversion.Markdown);
        Assert.Contains("Fund is Nest Higher Risk; Charge is 0.30%", conversion.Markdown);

        // The header row is not narrated as if it were data.
        Assert.DoesNotContain("Fund is Fund", conversion.Markdown);
    }
}
