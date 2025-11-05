using Maliev.CurrencyService.Api.Models.Currencies;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Currency metadata service interface
/// </summary>
/// <remarks>
/// User Story 1: Provides currency metadata lookup and country-to-currency resolution.
/// User Story 5: Extends with admin CRUD operations for currency management.
/// </remarks>
public interface ICurrencyService
{
    /// <summary>
    /// Gets paginated list of all currencies
    /// </summary>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of items per page (1-200)</param>
    /// <param name="isActive">Optional filter by active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated currency response</returns>
    Task<PaginatedCurrencyResponse> GetAllAsync(
        int page = 1,
        int pageSize = 50,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets currency by country code (ISO2 or ISO3)
    /// </summary>
    /// <param name="countryCode">Country code in ISO2 (e.g., "TH") or ISO3 (e.g., "THA") format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency response or null if country not found</returns>
    Task<CurrencyResponse?> GetByCountryCodeAsync(
        string countryCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets currency by code
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency response or null if not found</returns>
    Task<CurrencyResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets currency by ID
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Currency response or null if not found</returns>
    Task<CurrencyResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new currency
    /// </summary>
    /// <param name="request">Currency creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created currency response</returns>
    /// <remarks>
    /// User Story 5: Admin operation requiring JWT authentication.
    /// Invalidates currency list cache after creation.
    /// </remarks>
    Task<CurrencyResponse> CreateAsync(
        CreateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing currency
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="request">Currency update request with version</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated currency response or null if not found</returns>
    /// <remarks>
    /// User Story 5: Admin operation with optimistic concurrency control via Version.
    /// Throws DbUpdateConcurrencyException if version mismatch.
    /// Invalidates currency caches after update.
    /// </remarks>
    Task<CurrencyResponse?> UpdateAsync(
        string code,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing currency by ID
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="request">Currency update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated currency response or null if not found</returns>
    /// <remarks>
    /// User Story 5: Admin operation for GUID-based updates.
    /// Invalidates currency caches after update.
    /// </remarks>
    Task<CurrencyResponse?> UpdateByIdAsync(
        Guid id,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a currency (soft delete by setting IsActive=false)
    /// </summary>
    /// <param name="code">Currency code (ISO 4217)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    /// <remarks>
    /// User Story 5: Admin operation. Performs soft delete to preserve referential integrity.
    /// Invalidates currency caches after deletion.
    /// </remarks>
    Task<bool> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a currency by ID (soft delete by setting IsActive=false)
    /// </summary>
    /// <param name="id">Currency GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    /// <remarks>
    /// User Story 5: Admin operation for GUID-based deletion. Performs soft delete.
    /// Throws InvalidOperationException if currency has dependencies (country mappings).
    /// Invalidates currency caches after deletion.
    /// </remarks>
    Task<bool> DeleteByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
