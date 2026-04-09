using System.ComponentModel.DataAnnotations;

namespace OcrApi.Models;

// ── Create ──────────────────────────────────────────────────────────────────

public class OcrPropertyCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DataType { get; set; } = string.Empty;

    public string? SearchHeuristic { get; set; }

    public bool IsActive { get; set; } = true;
}

// ── Update ───────────────────────────────────────────────────────────────────

public class OcrPropertyUpdateDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? DataType { get; set; }

    public string? SearchHeuristic { get; set; }

    public bool? IsActive { get; set; }
}

// ── Response ─────────────────────────────────────────────────────────────────

public class OcrPropertyResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? SearchHeuristic { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
