using VendorRisk.Domain.Risk;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Domain.Rules;

/// <summary>
/// Section 5, operational factors: "Slow ticket resolution -&gt; Moderate risk".
///
/// NOT IMPLEMENTED: the case study defines no ticket-resolution field on VendorProfile and the
/// sample dataset carries no such value, so there is nothing to evaluate. The rule is registered
/// and always returns null, which keeps it visible in the rule set and makes it a one-line change
/// once the data exists. See the Missing Code Notice in the README.
/// </summary>
public sealed class SlowTicketResolutionRule : RiskRuleBase
{
    public override string RuleId => "SlowTicketResolution";

    public override RiskCategory Category => RiskCategory.Operational;

    public override RiskLevel Level => RiskLevel.Medium;

    protected override string Explanation => "Slow ticket resolution";

    // TODO: add a ticket-resolution measure to VendorProfile (e.g. AvgTicketResolutionHours)
    // and compare it against a configured threshold.
    protected override bool IsTriggered(VendorProfile vendor) => false;
}
