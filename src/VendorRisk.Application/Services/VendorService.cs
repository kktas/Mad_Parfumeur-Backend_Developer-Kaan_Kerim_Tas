using Microsoft.Extensions.Logging;
using VendorRisk.Application.Abstractions;
using VendorRisk.Application.Dtos;
using VendorRisk.Application.Mapping;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Services;

/// <summary>
/// Orchestrates vendor CRUD and assessment retrieval. Assessments are read cache-aside and the
/// cache entry is invalidated whenever the vendor's inputs change or the vendor is removed.
/// </summary>
public sealed class VendorService : IVendorService
{
    private static readonly TimeSpan AssessmentCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IVendorRepository _repository;
    private readonly IRiskScoringEngine _scoringEngine;
    private readonly ICacheService _cache;
    private readonly ILogger<VendorService> _logger;
    private readonly TimeProvider _timeProvider;

    public VendorService(
        IVendorRepository repository,
        IRiskScoringEngine scoringEngine,
        ICacheService cache,
        ILogger<VendorService> logger,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _scoringEngine = scoringEngine ?? throw new ArgumentNullException(nameof(scoringEngine));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<VendorResponse> CreateAsync(CreateVendorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureNameIsFreeAsync(request.Name, excludeVendorId: null, cancellationToken);

        var vendor = request.ToDomain(_timeProvider.GetUtcNow().UtcDateTime);
        var created = await _repository.AddAsync(vendor, cancellationToken);

        _logger.LogInformation("Created vendor {VendorId} ({VendorName})", created.Id, created.Name);

        return created.ToResponse();
    }

    public async Task<VendorResponse?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var vendor = await _repository.GetByIdAsync(id, cancellationToken);

        return vendor?.ToResponse();
    }

    public async Task<PagedResponse<VendorResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(page, pageSize, cancellationToken);
        var total = await _repository.CountAsync(cancellationToken);

        return new PagedResponse<VendorResponse>
        {
            Items = [.. items.Select(vendor => vendor.ToResponse())],
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<VendorResponse?> UpdateAsync(int id, UpdateVendorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vendor = await _repository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return null;
        }

        // Excludes this vendor, so keeping its own name is not treated as a collision.
        await EnsureNameIsFreeAsync(request.Name, excludeVendorId: id, cancellationToken);

        request.ApplyTo(vendor, _timeProvider.GetUtcNow().UtcDateTime);
        await _repository.UpdateAsync(vendor, cancellationToken);

        // The inputs changed, so any cached assessment is now stale.
        await _cache.RemoveAsync(CacheKeys.Assessment(id), cancellationToken);

        _logger.LogInformation("Updated vendor {VendorId} and invalidated its cached assessment", id);

        return vendor.ToResponse();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var vendor = await _repository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return false;
        }

        await _repository.DeleteAsync(vendor, cancellationToken);
        await _cache.RemoveAsync(CacheKeys.Assessment(id), cancellationToken);

        _logger.LogInformation("Deleted vendor {VendorId} and invalidated its cached assessment", id);

        return true;
    }

    public async Task<RiskAssessmentResponse?> GetRiskAssessmentAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Assessment(id);

        var cached = await _cache.GetAsync<RiskAssessmentResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Assessment cache hit for vendor {VendorId}", id);
            return cached;
        }

        var vendor = await _repository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return null;
        }

        var response = _scoringEngine.Evaluate(vendor).ToResponse(vendor.Name);
        await _cache.SetAsync(cacheKey, response, AssessmentCacheTtl, cancellationToken);

        _logger.LogDebug("Assessment cache miss for vendor {VendorId}; computed and cached", id);

        return response;
    }

    public async Task<VendorComparisonResponse> CompareAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var distinctIds = ids.Distinct().ToList();
        var vendors = await _repository.GetByIdsAsync(distinctIds, cancellationToken);
        var byId = vendors.ToDictionary(vendor => vendor.Id);

        var response = new VendorComparisonResponse
        {
            // Preserve the caller's ordering so the dashboard columns match the requested ids.
            NotFoundIds = [.. distinctIds.Where(id => !byId.ContainsKey(id))]
        };

        foreach (var id in distinctIds)
        {
            if (!byId.TryGetValue(id, out var vendor))
            {
                continue;
            }

            response.Vendors.Add(new VendorComparisonItem
            {
                Vendor = vendor.ToResponse(),
                Assessment = _scoringEngine.Evaluate(vendor).ToResponse(vendor.Name)
            });
        }

        return response;
    }

    /// <summary>
    /// Rejects a name another vendor already holds. This produces a clean 409 before the database
    /// is touched; the unique index behind it is what actually guarantees the invariant.
    /// </summary>
    private async Task EnsureNameIsFreeAsync(string name, int? excludeVendorId, CancellationToken cancellationToken)
    {
        var candidate = (name ?? string.Empty).Trim();

        if (await _repository.NameExistsAsync(candidate, excludeVendorId, cancellationToken))
        {
            _logger.LogInformation("Rejected vendor name {VendorName}: already taken", candidate);

            throw new DuplicateVendorNameException(candidate);
        }
    }
}
