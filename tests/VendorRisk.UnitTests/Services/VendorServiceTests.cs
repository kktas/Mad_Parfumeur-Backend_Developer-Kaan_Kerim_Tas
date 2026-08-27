using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VendorRisk.Application.Abstractions;
using VendorRisk.Application.Dtos;
using VendorRisk.Application.Services;
using VendorRisk.Domain.Vendors;
using VendorRisk.UnitTests.TestSupport;

namespace VendorRisk.UnitTests.Services;

public class VendorServiceTests
{
    private readonly Mock<IVendorRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<ICacheService> _cache = new(MockBehavior.Strict);

    private VendorService CreateSut() => new(
        _repository.Object,
        EngineFactory.Create(),
        _cache.Object,
        NullLogger<VendorService>.Instance);

    [Fact]
    public async Task CreateAsync_persists_the_vendor_and_returns_it()
    {
        _repository
            .Setup(repository => repository.AddAsync(It.IsAny<VendorProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfile vendor, CancellationToken _) =>
            {
                vendor.Id = 7;
                return vendor;
            });

        var created = await CreateSut().CreateAsync(new CreateVendorRequest
        {
            Name = "  TechPlus Solutions  ",
            FinancialHealth = 78,
            SlaUptime = 93,
            MajorIncidents = 1,
            SecurityCerts = ["ISO27001", "  ", "SOC2 "],
            Documents = new VendorDocumentsDto { ContractValid = true, PrivacyPolicyValid = false, PentestReportValid = true }
        });

        Assert.Equal(7, created.Id);
        Assert.Equal("TechPlus Solutions", created.Name);           // trimmed
        Assert.Equal(["ISO27001", "SOC2"], created.SecurityCerts);  // blanks dropped, entries trimmed
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAsync_returns_null_when_the_vendor_does_not_exist()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfile?)null);

        Assert.Null(await CreateSut().GetAsync(99));
    }

    [Fact]
    public async Task GetRiskAssessmentAsync_returns_the_cached_assessment_without_touching_the_repository()
    {
        var cached = new RiskAssessmentResponse { VendorId = 1, RiskLevel = "High", Reason = "cached" };

        _cache
            .Setup(cache => cache.GetAsync<RiskAssessmentResponse>(CacheKeys.Assessment(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var assessment = await CreateSut().GetRiskAssessmentAsync(1);

        Assert.Same(cached, assessment);
        // MockBehavior.Strict means an unexpected repository call would already have thrown; this
        // states the intent explicitly.
        _repository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRiskAssessmentAsync_computes_and_caches_on_a_miss()
    {
        var vendor = new VendorBuilder().WithId(1).WithSlaUptime(93).WithDocuments(privacyPolicyValid: false).Build();

        _cache
            .Setup(cache => cache.GetAsync<RiskAssessmentResponse>(CacheKeys.Assessment(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskAssessmentResponse?)null);
        _repository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _cache
            .Setup(cache => cache.SetAsync(
                CacheKeys.Assessment(1),
                It.IsAny<RiskAssessmentResponse>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var assessment = await CreateSut().GetRiskAssessmentAsync(1);

        Assert.NotNull(assessment);
        Assert.Equal("High", assessment.RiskLevel);
        Assert.Equal("SLA below 95% (High) + Privacy policy expired (Medium)", assessment.Reason);
        Assert.Equal(0d, assessment.RiskScore);
        _cache.VerifyAll();
    }

    [Fact]
    public async Task GetRiskAssessmentAsync_does_not_cache_a_missing_vendor()
    {
        _cache
            .Setup(cache => cache.GetAsync<RiskAssessmentResponse>(CacheKeys.Assessment(404), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskAssessmentResponse?)null);
        _repository
            .Setup(repository => repository.GetByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfile?)null);

        Assert.Null(await CreateSut().GetRiskAssessmentAsync(404));

        _cache.Verify(
            cache => cache.SetAsync(
                It.IsAny<string>(), It.IsAny<RiskAssessmentResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_invalidates_the_cached_assessment()
    {
        var vendor = new VendorBuilder().WithId(3).Build();

        _repository
            .Setup(repository => repository.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository
            .Setup(repository => repository.UpdateAsync(vendor, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache
            .Setup(cache => cache.RemoveAsync(CacheKeys.Assessment(3), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updated = await CreateSut().UpdateAsync(3, new UpdateVendorRequest
        {
            Name = "Renamed Vendor",
            FinancialHealth = 40,
            SlaUptime = 88,
            MajorIncidents = 4,
            SecurityCerts = [],
            Documents = new VendorDocumentsDto()
        });

        Assert.NotNull(updated);
        Assert.Equal("Renamed Vendor", updated.Name);
        _cache.Verify(cache => cache.RemoveAsync(CacheKeys.Assessment(3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_and_leaves_the_cache_alone_when_the_vendor_is_missing()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfile?)null);

        Assert.Null(await CreateSut().UpdateAsync(99, new UpdateVendorRequest { Name = "Nobody" }));

        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_vendor_and_invalidates_the_cache()
    {
        var vendor = new VendorBuilder().WithId(4).Build();

        _repository
            .Setup(repository => repository.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository
            .Setup(repository => repository.DeleteAsync(vendor, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache
            .Setup(cache => cache.RemoveAsync(CacheKeys.Assessment(4), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.True(await CreateSut().DeleteAsync(4));
        _cache.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_when_the_vendor_is_missing()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfile?)null);

        Assert.False(await CreateSut().DeleteAsync(99));
    }

    [Fact]
    public async Task ListAsync_reports_the_paging_envelope()
    {
        _repository
            .Setup(repository => repository.ListAsync(2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VendorBuilder().WithId(6).Build()]);
        _repository
            .Setup(repository => repository.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var page = await CreateSut().ListAsync(2, 5);

        Assert.Single(page.Items);
        Assert.Equal(11, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task CompareAsync_keeps_the_requested_order_and_reports_unknown_ids()
    {
        // The repository returns rows in its own order; the service must re-order them to match
        // the ids the caller asked for, so dashboard columns line up with the request.
        _repository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VendorBuilder().WithId(2).WithName("Second").Build(),
                new VendorBuilder().WithId(1).WithName("First").Build()
            ]);

        var comparison = await CreateSut().CompareAsync([1, 2, 99, 1]);

        Assert.Equal(["First", "Second"], comparison.Vendors.Select(item => item.Vendor.Name));
        Assert.Equal([99], comparison.NotFoundIds);
    }
}
