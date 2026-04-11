using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcrApi.Models;
using OcrApi.Repositories;

namespace OcrApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OcrPropertiesController : ControllerBase
{
    private readonly IOcrPropertyRepository _repository;

    public OcrPropertiesController(IOcrPropertyRepository repository)
    {
        _repository = repository;
    }

    // GET /api/ocrproperties
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OcrPropertyResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OcrPropertyResponseDto>>> GetAll()
    {
        var properties = await _repository.GetAllAsync();
        return Ok(properties.Select(ToResponseDto));
    }

    // GET /api/ocrproperties/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OcrPropertyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OcrPropertyResponseDto>> GetById(int id)
    {
        var property = await _repository.GetByIdAsync(id);
        if (property is null)
            return NotFound();

        return Ok(ToResponseDto(property));
    }

    // POST /api/ocrproperties
    [HttpPost]
    [ProducesResponseType(typeof(OcrPropertyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OcrPropertyResponseDto>> Create([FromBody] OcrPropertyCreateDto dto)
    {
        var entity = new OcrProperty
        {
            Name            = dto.Name,
            DataType        = dto.DataType,
            SearchHeuristic = dto.SearchHeuristic,
            IsActive        = dto.IsActive,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(entity);
        var response = ToResponseDto(created);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    // PUT /api/ocrproperties/{id}
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OcrPropertyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OcrPropertyResponseDto>> Update(int id, [FromBody] OcrPropertyUpdateDto dto)
    {
        // Fetch current values so we can apply only the fields that were supplied.
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null)
            return NotFound();

        var patch = new OcrProperty
        {
            Name            = dto.Name            ?? existing.Name,
            DataType        = dto.DataType        ?? existing.DataType,
            SearchHeuristic = dto.SearchHeuristic ?? existing.SearchHeuristic,
            IsActive        = dto.IsActive        ?? existing.IsActive
        };

        var updated = await _repository.UpdateAsync(id, patch);
        if (updated is null)
            return NotFound();

        return Ok(ToResponseDto(updated));
    }

    // DELETE /api/ocrproperties/{id}
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    // ── Private mapping helper ────────────────────────────────────────────────

    private static OcrPropertyResponseDto ToResponseDto(OcrProperty p) => new()
    {
        Id              = p.Id,
        Name            = p.Name,
        DataType        = p.DataType,
        SearchHeuristic = p.SearchHeuristic,
        IsActive        = p.IsActive,
        CreatedAt       = p.CreatedAt,
        UpdatedAt       = p.UpdatedAt
    };
}
