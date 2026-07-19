using Maliev.CurrencyService.Application.DTOs.Currencies;

namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Currency metadata service interface.
/// </summary>
/// <remarks>
/// User Story 1: Provides currency metadata lookup and country-to-currency resolution.
/// User Story 5: Extends with admin CRUD operations for currency management.
/// </remarks>
public interface ICurrencyService
{
    /// <summary>
    /// Gets a paginated list of all currencies.
    /// </summary>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Number of items per page (1-200).</param>
    /// <param name="isActive">Optional filter by active status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated currency response.</returns>
    Task<PaginatedCurrencyResponse> GetAllAsync(
        int page = 1,
        int pageSize = 50,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary currency for a given country code (ISO2 or ISO3).
    /// </summary>
    /// <param name="countryCode">Country code in ISO2 (e.g., "TH") or ISO3 (e.g., "THA") format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The currency response, or null if not found.</returns>
    Task<CurrencyResponse?> GetByCountryCodeAsync(
        string countryCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a currency by its ISO 4217 code.
    /// </summary>
    /// <param name="code">The 3-letter ISO 4217 currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The currency response, or null if not found.</returns>
    Task<CurrencyResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a currency by its unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The currency response, or null if not found.</returns>
    Task<CurrencyResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new currency.
    /// </summary>
    /// <param name="request">Currency creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created currency response.</returns>
    /// <remarks>
    /// User Story 5: Admin operation requiring JWT authentication.
    /// Invalidates currency list cache after creation.
    /// </remarks>
    Task<CurrencyResponse> CreateAsync(
        CreateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing currency by code.
    /// </summary>
    /// <param name="code">The ISO 4217 currency code.</param>
    /// <param name="request">Currency update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated currency response, or null if not found.</returns>
    /// <remarks>
    /// User Story 5: Admin operation with optimistic concurrency control via Version.
    /// Throws DbUpdateConcurrencyException on version mismatch.
    /// </remarks>
    Task<CurrencyResponse?> UpdateAsync(
        string code,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing currency by unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="request">Currency update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated currency response, or null if not found.</returns>
    Task<CurrencyResponse?> UpdateByIdAsync(
        Guid id,
        UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a currency by setting IsActive to false.
    /// </summary>
    /// <param name="code">The ISO 4217 currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the currency was found and deleted, otherwise false.</returns>
    Task<bool> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a currency by unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the currency was found and deleted, otherwise false.</returns>
    /// <remarks>Throws InvalidOperationException if the currency has country mappings.</remarks>
    Task<bool> DeleteByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a currency by unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false if not found.</returns>
    Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a currency by unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false if not found.</returns>
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
