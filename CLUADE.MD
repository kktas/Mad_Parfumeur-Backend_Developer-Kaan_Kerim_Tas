CASE STUDY: Vendor Risk Scoring Engine (Rule-Based Edition)
1. Task Overview
The goal of this assignment is to design a rule-based vendor risk scoring system that produces a transparent, explainable, and consistent Vendor Risk Score based on financial stability, operational performance, security compliance, and documentation validity.
The system must operate on an explainable decision engine where each risk score includes a clear rationale describing why the vendor received that score.

2. User Experience Flow
    1. Vendor Profile Setup
        The user completes a short vendor assessment form:
        -	What is the vendor’s financial health score?
        -	What is the SLA uptime percentage?
        -	Does the vendor hold security certifications (ISO27001, SOC2, PCI)?
        -	Are required compliance documents valid?
        -	How many major incidents occurred in the last 12 months?
    2. Profile Derivation
        The system interprets vendor data across three dimensions:
        -	Financial Health: Cash flow stability, credit risk, debt ratio
        -	Operational Reliability: SLA performance, incidents, ticket resolution speed
        -	Security & Compliance: Certificates, risk audits, document validity
    3. Recommendation Generation
        - Retrieve vendor profile data
        - Match risk categories based on impact & probability
        - Compute a score using the Risk Similarity Matrix
        - Determine overall risk level (Low / Medium / High / Critical)
    4. Result Presentation
    Risk assessments include explanations such as:
    “Missing ISO27001 and an SLA below 95% significantly increase the operational and security risk levels.”
3. Technologies
Layer	|Technology
_____________________
Backend	|.NET 8 Web API, Docker
Database|	PostgreSQL
Logging	|Serilog (JSON Sink), ELK
Testing	|xUnit + Moq

4. Domain Model
VendorProfile
{
  "id": 42,
  "name": "TechPlus Solutions",
  "financialHealth": 78,
  "slaUptime": 93,
  "majorIncidents": 1,
  "securityCerts": ["ISO27001"],
  "documents": {
    "contractValid": true,
    "privacyPolicyValid": false,
    "pentestReportValid": true
  }
}
RiskAssessment
{
  "vendorId": 42,
  "riskScore": 0.67,
  "riskLevel": "Medium",
5. Rule-Based Decision Engine
Financial Factors
Condition	Risk Impact
FinancialHealth < 50	High risk
FinancialHealth > 80	Low risk

Operational Factors
Condition	Risk Effect
SLA uptime < 95%	High operational risk
Major incidents > 2 (last 12 months)	High risk
Slow ticket resolution	Moderate risk
Security & Compliance
Condition	Risk Effect
Missing ISO27001	High security risk
Privacy Policy expired	Moderate compliance risk
Failed penetration test	Critical risk



6. Risk Similarity Matrix (Summary)
Risk Item	Similar Risks
SLA drop	downtime (0.87), slow ticketing (0.83)
expired privacy policy	missing NDA (0.81), outdated contract (0.78)
missing ISO27001	weak access control (0.84), no encryption policy (0.79)

Full table: RiskFactorMatrix.json

7. Scoring Formula
FinalScore = 
  (FinancialRisk * 0.4) + 
  (OperationalRisk * 0.3) + 
  (SecurityComplianceRisk * 0.3)


Vendor	Financial	Operational	Security	FinalScore
TechPlus	0.45	0.62	0.77	0.67


8. Example API Usage
POST /api/vendor
{
  "name": "TechPlus Solutions",
  "financialHealth": 78,
  "slaUptime": 93,
  "majorIncidents": 1,
  "securityCerts": [],
  "documents": {
    "contractValid": true,
    "privacyPolicyValid": false
  }
}
GET /api/vendor/42/risk
{
  "riskScore": 0.74,
  "riskLevel": "High",
  "reason": "Missing ISO27001 + Privacy policy expired + SLA < 95%"
}

9. Seed Data
- sample vendors
- risk factors
- Full similarity matrix dataset: RiskFactorMatrix.json
10. Acceptance Criteria
- Vendor database includes financial, operational, and security fields
- Rule-based scoring engine implemented (RuleEngineService)
- Each risk score includes a human-readable explanation
- Interface-based architecture for testability
- Serilog logging enabled
- README includes setup, dataset, and example requests
11. Bonus Features
- Redis caching for frequently queried vendors
- Vendor Comparison Dashboard
- Seasonal risk adjustments (optional — e.g., logistics seasonality)

12. Evaluation Criteria
Area	Evaluation Metric
Architecture Design	Layered structure, DI usage
Code Quality	SOLID, naming, error handling
Domain Understanding	Risk categories, weighting
Algorithmic Consistency	Rule logic, weighting stability
User Experience	Clear risk explanations
Documentation	README, datasets, setup guide


13. Deliverables & Timeline
-	Upload Path: Git repository and share with us
-	Missing Code Notice: If any module or dataset is incomplete, it must be documented in the README with partial implementation or stubs ensuring successful build.
EK A– RiskFactorMatrix.json
{
  "financialRisk": {
    "lowCashFlow": { "highDebtRatio": 0.88, "latePayments": 0.82, "weakRevenueStability": 0.79 },
    "highDebtRatio": { "lowCashFlow": 0.88, "creditDowngrade": 0.84, "liquidityIssues": 0.80 },
    "creditDowngrade": { "liquidityIssues": 0.86, "latePayments": 0.81, "weakRevenueStability": 0.77 }
  },
  "operationalRisk": {
    "slaDrop": { "downtime": 0.87, "slowTicketResolution": 0.83, "serviceInstability": 0.79 },
    "downtime": { "slaDrop": 0.87, "infrastructureFailure": 0.82, "lateDeliveries": 0.76 },
    "majorIncident": { "recurringIncidents": 0.88, "serviceOutage": 0.85, "securityIncident": 0.80 },
    "slowTicketResolution": { "slaDrop": 0.83, "lateIssueEscalation": 0.80, "supportCapacityIssues": 0.78 }
  },
  "securityRisk": {
    "missingISO27001": { "weakAccessControl": 0.84, "noEncryptionPolicy": 0.79, "failedAudit": 0.76 },
    "failedPenTest": { "internalVulnerabilities": 0.88, "weakFirewallRules": 0.83, "missingPatching": 0.79 },
    "weakAccessControl": { "missingISO27001": 0.84, "weakPasswordPolicy": 0.81, "privilegeMisuseRisk": 0.75 },
    "noEncryptionPolicy": { "dataLeakRisk": 0.86, "weakAccessControl": 0.79, "missingISO27001": 0.77 }
  },
  "complianceRisk": {
    "expiredPrivacyPolicy": { "missingNDA": 0.81, "expiredContract": 0.78, "gdprConflict": 0.75 },
    "missingNDA": { "expiredPrivacyPolicy": 0.81, "missingContract": 0.79, "weakLegalCoverage": 0.76 },
    "gdprConflict": { "expiredPrivacyPolicy": 0.75, "dataRetentionIssues": 0.82, "insufficientComplianceDocs": 0.80 },
    "expiredContract": { "missingNDA": 0.78, "expiredPrivacyPolicy": 0.78, "contractDisputeRisk": 0.74 }
  }
}
Ek B – SampleVendorData.json
{
  "Vendors": [
    {
      "id": 1,
      "name": "TechPlus Solutions",
      "financialHealth": 78,
      "slaUptime": 93,
      "majorIncidents": 1,
      "securityCerts": ["ISO27001"],
      "documents": { "contractValid": true, "privacyPolicyValid": false, "pentestReportValid": true }
    },

    {
      "id": 2,
      "name": "NovaLog Logistics",
      "financialHealth": 55,
      "slaUptime": 88,
      "majorIncidents": 3,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": false }
    },

    {
      "id": 3,
      "name": "SecurePay Financial",
      "financialHealth": 90,
      "slaUptime": 97,
      "majorIncidents": 0,
      "securityCerts": ["ISO27001", "PCI-DSS"],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": true }
    },

    {
      "id": 4,
      "name": "AlphaCloud Hosting",
      "financialHealth": 62,
      "slaUptime": 89,
      "majorIncidents": 2,
      "securityCerts": ["SOC2"],
      "documents": { "contractValid": true, "privacyPolicyValid": false, "pentestReportValid": false }
    },

    {
      "id": 5,
      "name": "TrustCom IT Services",
      "financialHealth": 48,
      "slaUptime": 92,
      "majorIncidents": 4,
      "securityCerts": [],
      "documents": { "contractValid": false, "privacyPolicyValid": false, "pentestReportValid": false }
    },

    {
      "id": 6,
      "name": "GlobalTrans Freight",
      "financialHealth": 70,
      "slaUptime": 95,
      "majorIncidents": 1,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": false }
    },

    {
      "id": 7,
      "name": "Skyline Software",
      "financialHealth": 88,
      "slaUptime": 99,
      "majorIncidents": 0,
      "securityCerts": ["ISO27001"],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": true }
    },

    {
      "id": 8,
      "name": "DataBridge Analytics",
      "financialHealth": 43,
      "slaUptime": 86,
      "majorIncidents": 2,
      "securityCerts": [],
      "documents": { "contractValid": false, "privacyPolicyValid": false, "pentestReportValid": false }
    },

    {
      "id": 9,
      "name": "CargoLine Transport",
      "financialHealth": 59,
      "slaUptime": 92,
      "majorIncidents": 1,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": false }
    },

    {
      "id": 10,
      "name": "HexaCloud DevOps",
      "financialHealth": 75,
      "slaUptime": 96,
      "majorIncidents": 0,
      "securityCerts": ["ISO27001", "SOC2"],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": true }
    },

    {
      "id": 11,
      "name": "VisionTech Support",
      "financialHealth": 52,
      "slaUptime": 90,
      "majorIncidents": 3,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": false, "pentestReportValid": false }
    },

    {
      "id": 12,
      "name": "PrimeNet Security",
      "financialHealth": 83,
      "slaUptime": 94,
      "majorIncidents": 0,
      "securityCerts": ["ISO27001"],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": false }
    },

    {
      "id": 13,
      "name": "Velocity Warehousing",
      "financialHealth": 61,
      "slaUptime": 91,
      "majorIncidents": 2,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": false, "pentestReportValid": false }
    },

    {
      "id": 14,
      "name": "Orion Network Ops",
      "financialHealth": 95,
      "slaUptime": 99,
      "majorIncidents": 0,
      "securityCerts": ["ISO27001", "SOC2"],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": true }
    },

    {
      "id": 15,
      "name": "BlueWave Consulting",
      "financialHealth": 67,
      "slaUptime": 87,
      "majorIncidents": 1,
      "securityCerts": [],
      "documents": { "contractValid": true, "privacyPolicyValid": true, "pentestReportValid": false }
    }
  ]
}
