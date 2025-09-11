using Maliev.CurrencyService.Api.Models;

namespace Maliev.CurrencyService.Api.Services;

public interface ICurrencyService
{
    Task<CurrencyDto?> GetByIdAsync(int id);
    Task<CurrencyDto?> GetByShortNameAsync(string shortName);
    Task<PagedResult<CurrencyDto>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<CurrencyDto> CreateAsync(CreateCurrencyRequest request);
    Task<CurrencyDto?> UpdateAsync(int id, UpdateCurrencyRequest request);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<string>> GetCurrencyCodesAsync();
}