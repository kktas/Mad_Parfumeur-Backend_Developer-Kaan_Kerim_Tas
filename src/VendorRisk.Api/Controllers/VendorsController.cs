using Microsoft.AspNetCore.Mvc;
using VendorRisk.Application.Dtos;
using VendorRisk.Application.Services;

namespace VendorRisk.Api.Controllers;

/// <summary>Vendor profiles and their risk assessments (case study section 8).</summary>
[ApiController]
[Route("api/vendor")]
[Produces("application/json")]
public sealed class VendorsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxComparisonIds = 10;

    private readonly IVendorService _vendorService;
    private readonly ILogger<VendorsController> _logger;

    public VendorsController(IVendorService vendorService, ILogger<VendorsController> logger)
    {
        _vendorService = vendorService ?? throw new ArgumentNullException(nameof(vendorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Registers a vendor from the assessment form.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VendorResponse>> Create(
        [FromBody] CreateVendorRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _vendorService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Lists vendors, newest ids last.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VendorResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VendorResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        return Ok(await _vendorService.ListAsync(page, pageSize, cancellationToken));
    }

    /// <summary>Fetches a single vendor.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var vendor = await _vendorService.GetAsync(id, cancellationToken);

        return vendor is null ? VendorNotFound(id) : Ok(vendor);
    }

    /// <summary>Replaces a vendor's assessment inputs and invalidates its cached assessment.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorResponse>> Update(
        int id,
        [FromBody] UpdateVendorRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _vendorService.UpdateAsync(id, request, cancellationToken);

        return updated is null ? VendorNotFound(id) : Ok(updated);
    }

    /// <summary>Removes a vendor.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _vendorService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : VendorNotFound(id);
    }

    /// <summary>
    /// Returns the vendor's risk assessment: level, human-readable reason, and the rules behind it.
    /// riskScore is always 0 in this build; see the Missing Code Notice in the README.
    /// </summary>
    [HttpGet("{id:int}/risk")]
    [ProducesResponseType(typeof(RiskAssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskAssessmentResponse>> GetRisk(int id, CancellationToken cancellationToken)
    {
        var assessment = await _vendorService.GetRiskAssessmentAsync(id, cancellationToken);

        return assessment is null ? VendorNotFound(id) : Ok(assessment);
    }

    /// <summary>Compares several vendors side by side, backing the comparison dashboard.</summary>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(VendorComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VendorComparisonResponse>> Compare(
        [FromQuery] string? ids,
        CancellationToken cancellationToken)
    {
        if (!TryParseIds(ids, out var parsedIds, out var error))
        {
            return ValidationProblem(error);
        }

        _logger.LogInformation("Comparing vendors {VendorIds}", parsedIds);

        return Ok(await _vendorService.CompareAsync(parsedIds, cancellationToken));
    }

    /// <summary>Parses the comma-separated "ids" query parameter, e.g. ?ids=1,2,3.</summary>
    private static bool TryParseIds(string? ids, out List<int> parsedIds, out string error)
    {
        parsedIds = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ids))
        {
            error = "Provide at least one vendor id, e.g. ?ids=1,2,3.";
            return false;
        }

        foreach (var token in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, out var id))
            {
                error = $"'{token}' is not a valid vendor id.";
                return false;
            }

            parsedIds.Add(id);
        }

        if (parsedIds.Count == 0)
        {
            error = "Provide at least one vendor id, e.g. ?ids=1,2,3.";
            return false;
        }

        if (parsedIds.Distinct().Count() > MaxComparisonIds)
        {
            error = $"Compare at most {MaxComparisonIds} vendors at a time.";
            return false;
        }

        return true;
    }

    private ActionResult VendorNotFound(int id) =>
        NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Vendor not found",
            Detail = $"No vendor exists with id {id}.",
            Instance = HttpContext?.Request.Path
        });
}
