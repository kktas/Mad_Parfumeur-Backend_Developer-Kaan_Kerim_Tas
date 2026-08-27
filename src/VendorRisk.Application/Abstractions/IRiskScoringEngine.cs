using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Application.Abstractions;

/// <summary>
/// Turns a vendor profile into an explainable risk assessment.
/// </summary>
public interface IRiskScoringEngine
{
    RiskAssessment Evaluate(VendorProfile vendor);
}
