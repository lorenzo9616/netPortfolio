using OcrApi.Models;

namespace OcrApi.Repositories;

public interface IOcrPropertyRepository
{
    Task<IEnumerable<OcrProperty>> GetAllAsync();
    Task<OcrProperty?> GetByIdAsync(int id);
    Task<OcrProperty> CreateAsync(OcrProperty property);
    Task<OcrProperty?> UpdateAsync(int id, OcrProperty property);
    Task<bool> DeleteAsync(int id);
}
