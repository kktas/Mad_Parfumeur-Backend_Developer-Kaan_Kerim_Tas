using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using VendorRisk.Api.Controllers;
using VendorRisk.Application.Dtos;
using VendorRisk.Application.Services;

namespace VendorRisk.UnitTests.Controllers;

public class VendorsControllerTests
{
    private readonly Mock<IVendorService> _vendorService = new();

    private VendorsController CreateSut() =>
        new(_vendorService.Object, NullLogger<VendorsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task Create_returns_201_pointing_at_the_new_vendor()
    {
        _vendorService
            .Setup(service => service.CreateAsync(It.IsAny<CreateVendorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorResponse { Id = 16, Name = "TechPlus Solutions" });

        var result = await CreateSut().Create(new CreateVendorRequest { Name = "TechPlus Solutions" }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(VendorsController.GetById), created.ActionName);
        Assert.Equal(16, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task GetById_returns_404_with_a_problem_document_when_absent()
    {
        _vendorService
            .Setup(service => service.GetAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorResponse?)null);

        var result = await CreateSut().GetById(99, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Contains("99", problem.Detail);
    }

    [Fact]
    public async Task GetRisk_returns_the_assessment()
    {
        _vendorService
            .Setup(service => service.GetRiskAssessmentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskAssessmentResponse
            {
                VendorId = 1,
                RiskScore = 0d,
                RiskLevel = "High",
                Reason = "SLA below 95% (High) + Privacy policy expired (Medium)"
            });

        var result = await CreateSut().GetRisk(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var assessment = Assert.IsType<RiskAssessmentResponse>(ok.Value);
        Assert.Equal("High", assessment.RiskLevel);
        Assert.Equal(0d, assessment.RiskScore);
    }

    [Fact]
    public async Task GetRisk_returns_404_when_the_vendor_is_absent()
    {
        _vendorService
            .Setup(service => service.GetRiskAssessmentAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskAssessmentResponse?)null);

        var result = await CreateSut().GetRisk(99, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_returns_204_then_404()
    {
        _vendorService.Setup(service => service.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _vendorService.Setup(service => service.DeleteAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut();

        Assert.IsType<NoContentResult>(await sut.Delete(1, default));
        Assert.IsType<NotFoundObjectResult>(await sut.Delete(99, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,not-a-number")]
    [InlineData("1,2,3,4,5,6,7,8,9,10,11")]  // above the 10-vendor comparison cap
    public async Task Compare_rejects_malformed_id_lists(string? ids)
    {
        var result = await CreateSut().Compare(ids, default);

        Assert.IsType<ObjectResult>(result.Result);
        _vendorService.Verify(
            service => service.CompareAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Compare_parses_a_comma_separated_id_list()
    {
        _vendorService
            .Setup(service => service.CompareAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorComparisonResponse());

        var result = await CreateSut().Compare(" 1, 2 ,3 ", default);

        Assert.IsType<OkObjectResult>(result.Result);
        _vendorService.Verify(
            service => service.CompareAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2, 3 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task List_clamps_paging_arguments_to_sane_bounds()
    {
        _vendorService
            .Setup(service => service.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<VendorResponse>());

        await CreateSut().List(page: -5, pageSize: 5000);

        _vendorService.Verify(service => service.ListAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The controller relies on [ApiController] model validation rather than hand-rolled checks,
    /// so this asserts the DataAnnotations on the request DTO actually reject bad input.
    /// </summary>
    [Theory]
    [InlineData("", 50, 99, 0, false)]      // name required
    [InlineData("Valid Name", -1, 99, 0, false)]   // financialHealth below range
    [InlineData("Valid Name", 101, 99, 0, false)]  // financialHealth above range
    [InlineData("Valid Name", 50, 101, 0, false)]  // slaUptime above range
    [InlineData("Valid Name", 50, 99, -2, false)]  // negative incidents
    [InlineData("Valid Name", 50, 99, 0, true)]
    public void CreateVendorRequest_validation_matches_the_documented_ranges(
        string name, int financialHealth, decimal slaUptime, int majorIncidents, bool expectedValid)
    {
        var request = new CreateVendorRequest
        {
            Name = name,
            FinancialHealth = financialHealth,
            SlaUptime = slaUptime,
            MajorIncidents = majorIncidents
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }
}
