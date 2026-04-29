namespace OcrApi.Models;

// ── Analyze request (form fields from Next.js) ───────────────────────────────

public class AnalyzeDocumentRequest
{
    public int? CropX { get; set; }
    public int? CropY { get; set; }
    public int? CropWidth { get; set; }
    public int? CropHeight { get; set; }
}

// ── OCR engine response types (deserialized from Python service) ─────────────

public class OcrTextBlock
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
}

public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ExtractTextResponse
{
    public bool Success { get; set; }
    public int PageCount { get; set; }
    public List<OcrTextBlock> TextBlocks { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public float ProcessingTimeMs { get; set; }
}

// ── Field extraction types ────────────────────────────────────────────────────

public class ExtractedField
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public double Confidence { get; set; }
}

public class AnalyzeDocumentResponse
{
    public int DocumentId { get; set; }
    public List<ExtractedField> ExtractedFields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
}

// ── PUT /api/documents/{id}/fields body ──────────────────────────────────────

public class SavedFieldUpdateDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ManualOverride { get; set; }
}

// ── Text block DTO (returned to frontend) ────────────────────────────────────

public class TextBlockDto
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }
}

// ── GET /api/documents/{id} response ─────────────────────────────────────────

public class AnalysisResultDetailDto
{
    public int DocumentId { get; set; }
    public List<ExtractedFieldDto> ExtractedFields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public List<TextBlockDto> TextBlocks { get; set; } = new();
    public string? SignatureImage { get; set; }
}

public class ExtractedFieldDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public string? ManualOverride { get; set; }
    public double Confidence { get; set; }
}

// ── PATCH /api/documents/{id}/signature ──────────────────────────────────────

public class SignatureCaptureRequest
{
    public string ImageData { get; set; } = string.Empty;
}

public class SignatureCaptureResponse
{
    public string ImageData { get; set; } = string.Empty;
}
