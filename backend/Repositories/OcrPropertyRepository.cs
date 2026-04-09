using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;

namespace OcrApi.Repositories;

public class OcrPropertyRepository : IOcrPropertyRepository
{
    private readonly OcrDbContext _context;

    public OcrPropertyRepository(OcrDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<OcrProperty>> GetAllAsync()
    {
        return await _context.OcrProperties
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<OcrProperty?> GetByIdAsync(int id)
    {
        return await _context.OcrProperties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<OcrProperty> CreateAsync(OcrProperty property)
    {
        _context.OcrProperties.Add(property);
        await _context.SaveChangesAsync();
        return property;
    }

    public async Task<OcrProperty?> UpdateAsync(int id, OcrProperty property)
    {
        var existing = await _context.OcrProperties.FindAsync(id);
        if (existing is null)
            return null;

        existing.Name            = property.Name;
        existing.DataType        = property.DataType;
        existing.SearchHeuristic = property.SearchHeuristic;
        existing.IsActive        = property.IsActive;
        existing.UpdatedAt       = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _context.OcrProperties.FindAsync(id);
        if (existing is null)
            return false;

        _context.OcrProperties.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
