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
    private readonly Mock<ISecurityCertificateRepository> _certificates = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<ICacheService> _cache = new(MockBehavior.Strict);

    public VendorServiceTests()
    {
        // Stands in for the catalogue: every create and update resolves its codes through it, so
        // the behaviour is set up once rather than in each test that happens to write a vendor.
        _certificates
            .Setup(repository => repository.ResolveAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string>? codes, CancellationToken _) => Catalogue(codes));

        // The commit boundary. Every write goes through it, so tests assert on how often it is
        // called rather than on each repository saving for itself.
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private VendorService CreateSut() => new(
        _repository.Object,
        _certificates.Object,
        _unitOfWork.Object,
        EngineFactory.Create(),
        _cache.Object,
        NullLogger<VendorService>.Instance);

    /// <summary>One catalogue row per normalised code, as the repository returns once it has registered anything new.</summary>
    private static IReadOnlyList<SecurityCertificate> Catalogue(IEnumerable<string>? codes) =>
    [
        .. SecurityCertificates
            .Normalise(codes)
            .Select((code, index) => new SecurityCertificate { Id = index + 1, Code = code, Name = code })
    ];

    /// <summary>Default: the name is free, so uniqueness never blocks the test under inspection.</summary>
    private void NameIsFree() => _repository
        .Setup(repository => repository.NameExistsAsync(
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    [Fact]
    public async Task CreateAsync_persists_the_vendor_and_returns_it()
    {
        NameIsFree();
        // Add only stages the vendor; the database assigns its id when the unit of work commits.
        _repository
            .Setup(repository => repository.Add(It.IsAny<VendorProfile>()))
            .Callback((VendorProfile vendor) => vendor.Id = 7);

        var created = await CreateSut().CreateAsync(new CreateVendorRequest
        {
            Name = "  TechPlus Solutions  ",
            FinancialHealth = 78,
            SlaUptime = 93,
            MajorIncidents = 1,
            SecurityCerts = ["iso27001", "  ", " soc2 ", "ISO27001", "Iso27001"],
            Documents = new VendorDocumentsDto { ContractValid = true, PrivacyPolicyValid = false, PentestReportValid = true }
        });

        Assert.Equal(7, created.Id);
        Assert.Equal("TechPlus Solutions", created.Name);           // trimmed
        // Blanks dropped, entries trimmed and upper-cased, case-insensitive duplicates collapsed.
        Assert.Equal(["ISO27001", "SOC2"], created.SecurityCerts);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_name_another_vendor_already_holds()
    {
        _repository
            .Setup(repository => repository.NameExistsAsync("TechPlus Solutions", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateVendorNameException>(
            () => sut.CreateAsync(new CreateVendorRequest { Name = "  TechPlus Solutions  " }));

        // Rejected before anything is staged, and nothing is committed.
        _repository.Verify(repository => repository.Add(It.IsAny<VendorProfile>()), Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        NameIsFree();
        _repository
            .Setup(repository => repository.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository.Setup(repository => repository.Update(vendor));
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
        // An update is a full replacement, so clearing securityCerts unlinks the vendor's certificates.
        Assert.Empty(updated.SecurityCerts);
        Assert.Empty(vendor.Certificates);
        _cache.Verify(cache => cache.RemoveAsync(CacheKeys.Assessment(3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_lets_a_vendor_keep_its_own_name()
    {
        var vendor = new VendorBuilder().WithId(3).WithName("TechPlus Solutions").Build();

        _repository
            .Setup(repository => repository.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        // The check must exclude vendor 3 itself, or renaming nothing would look like a collision.
        _repository
            .Setup(repository => repository.NameExistsAsync("TechPlus Solutions", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository.Setup(repository => repository.Update(vendor));
        _cache
            .Setup(cache => cache.RemoveAsync(CacheKeys.Assessment(3), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updated = await CreateSut().UpdateAsync(3, new UpdateVendorRequest
        {
            Name = "TechPlus Solutions",
            FinancialHealth = 60,
            SlaUptime = 99
        });

        Assert.NotNull(updated);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_name_a_different_vendor_holds()
    {
        var vendor = new VendorBuilder().WithId(3).WithName("Original Name").Build();

        _repository
            .Setup(repository => repository.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository
            .Setup(repository => repository.NameExistsAsync("Skyline Software", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateVendorNameException>(
            () => sut.UpdateAsync(3, new UpdateVendorRequest { Name = "Skyline Software" }));

        Assert.Equal("Original Name", vendor.Name);   // unchanged
        _repository.Verify(repository => repository.Update(It.IsAny<VendorProfile>()), Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repository.Setup(repository => repository.Remove(vendor));
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
    public async Task CreateAsync_commits_the_vendor_and_its_new_certificates_together()
    {
        NameIsFree();
        _repository
            .Setup(repository => repository.Add(It.IsAny<VendorProfile>()))
            .Callback((VendorProfile vendor) => vendor.Id = 8);

        await CreateSut().CreateAsync(new CreateVendorRequest
        {
            Name = "Single Commit Vendor",
            SecurityCerts = ["ISO27001", "BRAND-NEW-CODE"]
        });

        // The catalogue row the second code registers is staged, not saved separately: one commit
        // covers the vendor, its links and that row.
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_leaves_the_cached_assessment_alone_when_the_commit_fails()
    {
        var vendor = new VendorBuilder().WithId(5).Build();

        NameIsFree();
        _repository
            .Setup(repository => repository.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository.Setup(repository => repository.Update(vendor));
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateVendorNameException("Taken Name"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateVendorNameException>(
            () => sut.UpdateAsync(5, new UpdateVendorRequest { Name = "Taken Name" }));

        // Nothing changed in the database, so evicting the cached assessment would only cost a
        // recomputation - and evicting before the commit would be a lie if it then failed.
        _cache.Verify(
            cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_leaves_the_cached_assessment_alone_when_the_commit_fails()
    {
        var vendor = new VendorBuilder().WithId(6).Build();

        _repository
            .Setup(repository => repository.GetByIdAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _repository.Setup(repository => repository.Remove(vendor));
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DeleteAsync(6));

        _cache.Verify(
            cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
