// Ported verbatim from IQFlow.Adapters.Ingest, apart from the namespace and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1": these are defect fixes for losses the pipeline analysis names and nothing downstream can
// recover. Kept byte-faithful on purpose — a "tidied" copy is a second implementation to keep in
// step with the original, and the fixes are the product of measurement rather than of taste.
using System.Text.Json.Serialization;

namespace IQWorkflow;

/// <summary>
/// DTOs mirroring the subset of docling-serve's response schema we need:
/// ConvertDocumentResponse -> ExportDocumentResponse -> DoclingDocument (json_content).
/// Field names use snake_case to match docling-serve's JSON verbatim.
/// </summary>
public sealed class ConvertDocumentResponse
{
    [JsonPropertyName("document")]
    public ExportDocumentResponse? Document { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errors")]
    public List<ErrorItem>? Errors { get; set; }

    [JsonPropertyName("processing_time")]
    public double ProcessingTime { get; set; }
}

public sealed class ErrorItem
{
    [JsonPropertyName("component_type")]
    public string? ComponentType { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public sealed class ExportDocumentResponse
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("md_content")]
    public string? MdContent { get; set; }

    [JsonPropertyName("json_content")]
    public DoclingDocument? JsonContent { get; set; }
}

public sealed class DoclingDocument
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("texts")]
    public List<DoclingTextItem> Texts { get; set; } = [];

    [JsonPropertyName("pictures")]
    public List<DoclingPictureItem> Pictures { get; set; } = [];

    [JsonPropertyName("tables")]
    public List<DoclingTableItem> Tables { get; set; } = [];

    [JsonPropertyName("key_value_items")]
    public List<DoclingKeyValueItem> KeyValueItems { get; set; } = [];

    [JsonPropertyName("form_items")]
    public List<DoclingFormItem> FormItems { get; set; } = [];
}

public sealed class DoclingTextItem
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("orig")]
    public string? Orig { get; set; }

    [JsonPropertyName("prov")]
    public List<DoclingProvenance>? Prov { get; set; }
}

public sealed class DoclingProvenance
{
    [JsonPropertyName("page_no")]
    public int PageNo { get; set; }

    [JsonPropertyName("bbox")]
    public DoclingBBox? BBox { get; set; }

    /// <summary>Start/end character offsets of this fragment within the parent item's <c>Text</c>.</summary>
    [JsonPropertyName("charspan")]
    public int[]? CharSpan { get; set; }
}

public sealed class DoclingBBox
{
    [JsonPropertyName("l")]
    public double L { get; set; }

    [JsonPropertyName("t")]
    public double T { get; set; }

    [JsonPropertyName("r")]
    public double R { get; set; }

    [JsonPropertyName("b")]
    public double B { get; set; }
}

public sealed class DoclingPictureItem
{
    [JsonPropertyName("captions")]
    public List<object>? Captions { get; set; }

    [JsonPropertyName("annotations")]
    public List<PictureAnnotation>? Annotations { get; set; }

    /// <summary>
    /// The picture itself, returned when <c>include_images</c> is on.
    /// </summary>
    /// <remarks>
    /// This is how a table pasted into a document as a picture gets read at all: Docling cannot
    /// extract structure from a bitmap, but it can hand the bitmap back.
    /// </remarks>
    [JsonPropertyName("image")]
    public DoclingImageRef? Image { get; set; }
}

/// <summary>An embedded image, as a data URI.</summary>
public sealed class DoclingImageRef
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("mimetype")]
    public string? MimeType { get; set; }
}

public sealed class PictureAnnotation
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public sealed class DoclingKeyValueItem
{
    [JsonPropertyName("graph")]
    public KeyValueGraph? Graph { get; set; }
}

public sealed class KeyValueGraph
{
    [JsonPropertyName("cells")]
    public List<KeyValueCell> Cells { get; set; } = [];

    [JsonPropertyName("links")]
    public List<KeyValueLink> Links { get; set; } = [];
}

public sealed class KeyValueCell
{
    [JsonPropertyName("cell_id")]
    public int CellId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class KeyValueLink
{
    [JsonPropertyName("source_cell_id")]
    public int SourceCellId { get; set; }

    [JsonPropertyName("target_cell_id")]
    public int TargetCellId { get; set; }
}

public sealed class DoclingFormItem
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class DoclingTableItem
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("captions")]
    public List<object>? Captions { get; set; }

    [JsonPropertyName("data")]
    public DoclingTableData? Data { get; set; }
}

public sealed class DoclingTableData
{
    [JsonPropertyName("num_rows")]
    public int NumRows { get; set; }

    [JsonPropertyName("num_cols")]
    public int NumCols { get; set; }

    [JsonPropertyName("grid")]
    public List<List<DoclingTableCell>> Grid { get; set; } = [];
}

public sealed class DoclingTableCell
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("row_span")]
    public int RowSpan { get; set; } = 1;

    [JsonPropertyName("col_span")]
    public int ColSpan { get; set; } = 1;

    [JsonPropertyName("start_row_offset_idx")]
    public int StartRow { get; set; }

    [JsonPropertyName("end_row_offset_idx")]
    public int EndRow { get; set; }

    [JsonPropertyName("start_col_offset_idx")]
    public int StartCol { get; set; }

    [JsonPropertyName("end_col_offset_idx")]
    public int EndCol { get; set; }

    [JsonPropertyName("column_header")]
    public bool ColumnHeader { get; set; }

    [JsonPropertyName("row_header")]
    public bool RowHeader { get; set; }

    [JsonPropertyName("row_section")]
    public bool RowSection { get; set; }
}
