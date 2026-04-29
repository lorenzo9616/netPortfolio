namespace OcrApi.Models;

public class OcrProperty
{
    public int Id { get; set; }

    /// <summary>Human-readable field name, e.g. "DOB", "Signature".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Expected value kind: "date", "string", "decimal", etc.</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Optional regex or keyword hint used during OCR extraction.</summary>
    public string? SearchHeuristic { get; set; }

    /// <summary>When true, SearchHeuristic is treated as a regex; otherwise plain keyword.</summary>
    public bool IsRegex { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
