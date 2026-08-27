using VendorRisk.Application.Dtos;

namespace VendorRisk.Application.Services;

/// <summary>Use cases behind the /api/vendor endpoints.</summary>
public interface IVendorService
{
    Task<VendorResponse> CreateAsync(CreateVendorRequest request, CancellationToken cancellationToken = default);

    Task<VendorResponse?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResponse<VendorResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<VendorResponse?> UpdateAsync(int id, UpdateVendorRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<RiskAssessmentResponse?> GetRiskAssessmentAsync(int id, CancellationToken cancellationToken = default);

    Task<VendorComparisonResponse> CompareAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
