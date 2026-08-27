# Vendor Risk Scoring Engine

A rule-based vendor risk scoring service built for the *Vendor Risk Scoring Engine (Rule-Based Edition)* case study. Every assessment is explainable: each risk level comes with the list of rules that produced it and a human-readable reason.

```http
GET /api/vendor/1/risk
```
```json
{
  "vendorId": 1,
  "vendorName": "TechPlus Solutions",
  "riskScore": 0,
  "riskLevel": "High",
  "reason": "SLA below 95% (High) + Privacy policy expired (Medium)"
}
```

> ### ⚠️ Missing Code Notice
>
> Per section 13 of the brief, the incomplete parts are documented here. The build succeeds, all tests pass, and every endpoint works — but:
>
> 1. **`riskScore` is always `0.0`.** The rules, risk levels and reasons are fully implemented; the *numeric* score from section 7 (`Financial × 0.4 + Operational × 0.3 + SecurityCompliance × 0.3`) is not. `riskLevel` is derived from the highest triggered rule instead of from score thresholds. The `// TODO` in [`RuleBasedRiskScoringEngine.cs`](src/VendorRisk.Application/Scoring/RuleBasedRiskScoringEngine.cs) marks exactly where the formula goes.
> 2. **`RiskFactorMatrix.json` is not consumed.** The full similarity matrix from appendix A ships in [`data/RiskFactorMatrix.json`](data/RiskFactorMatrix.json) but nothing reads it. It is intended to feed the numeric score once that lands.
> 3. **`SlowTicketResolutionRule` never fires.** Section 5 defines "Slow ticket resolution → Moderate risk", but neither the domain model nor the sample dataset carries a ticket-resolution measure. The rule is registered and tested, and returns `null` until such a field exists.
> 4. **"Failed penetration test" is inferred from `pentestReportValid`.** The dataset has no pass/fail result, only report validity, so an invalid or missing report is treated as a failed test. Because 10 of the 15 sample vendors have `pentestReportValid: false`, and that rule is Critical, **most of the seed data assesses as Critical**. This is the data, not a bug.
> 5. **`contractValid` has no rule.** Section 5 defines no condition for it, so none was invented. It is stored and returned by the API but never affects an assessment.
>
> One further note: the brief's own worked example is internally inconsistent. TechPlus at `0.45 / 0.62 / 0.77` yields **0.597** under the section 7 formula, not the **0.67** shown in the table. Whoever implements the numeric score should calibrate against the formula rather than that number.

---

## Quick start

### Docker (recommended)

```bash
cp .env.example .env      # optional; sensible defaults are built in
docker compose up --build
```

That starts the API, PostgreSQL and Redis. On first run the schema is migrated and the 15 sample vendors from appendix B are seeded.

| What | Where |
| --- | --- |
| Dashboard | <http://localhost:8080/> |
| Swagger UI | <http://localhost:8080/swagger> |
| Health check | <http://localhost:8080/health> |

The containers publish PostgreSQL on **5433** and Redis on **6380** rather than their default ports, so the stack does not collide with a PostgreSQL or Redis you already run locally. Override `POSTGRES_PORT` and `REDIS_PORT` in `.env` if those are taken too — only the host side moves, so nothing inside the compose network changes.

### Local development

Requires the .NET 8 SDK (or a newer SDK — `net8.0` builds fine under SDK 9/10) and a reachable PostgreSQL.

```bash
docker compose up -d postgres redis          # or bring your own PostgreSQL
dotnet run --project src/VendorRisk.Api
```

Redis is optional. It defaults to `localhost:6380`; clear `ConnectionStrings:Redis` to an empty string and the app registers a no-op cache and recomputes every assessment instead. Note that a configured Redis is also health-checked, so `/health` reports unhealthy if the connection string points at an instance that is not running.

### Tests

```bash
dotnet test
```

100 tests covering every rule boundary, the engine's roll-up and reason formatting, the service's cache behaviour, controller status codes, and a regression theory pinning all 15 seeded vendors.

---

## Architecture

```
src/
  VendorRisk.Domain/          Entities, risk primitives, the section 5 rule set. No dependencies.
  VendorRisk.Application/     Scoring engine, services, DTOs, abstractions.
  VendorRisk.Infrastructure/  EF Core + Npgsql, repository, Redis cache, seeding.
  VendorRisk.Api/             Controllers, DI, Serilog, Swagger, dashboard.
tests/
  VendorRisk.UnitTests/       xUnit + Moq.
```

Dependencies point inward only: `Api → Application → Domain` and `Infrastructure → Application → Domain`. The API references Infrastructure solely to wire up its composition root, so the application layer stays ignorant of EF Core and Redis.

Every boundary is an interface — `IRiskRule`, `IRiskScoringEngine`, `IVendorRepository`, `ICacheService`, `IVendorService` — which is what lets the unit tests run with mocks and no database.

### How scoring works

1. Every registered `IRiskRule` is evaluated against the vendor. A rule returns `null` when it does not fire.
2. **Overall level = the highest level among the rules that fired.** A vendor tripping nothing is `Low`.
3. **Reason** joins each finding as `Explanation (Level)` with ` + `, most severe first. Rules of equal severity keep DI registration order, so the string is deterministic.
4. Findings are grouped per dimension into `dimensions`, always covering all three categories.

Rules arrive by constructor injection (`IEnumerable<IRiskRule>`), so adding a rule means adding a class and one registration line in [`ApplicationServiceCollectionExtensions.cs`](src/VendorRisk.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs) — the engine never changes.

### The rule set (case study section 5)

| Rule | Condition | Level | Reason clause |
| --- | --- | --- | --- |
| `LowFinancialHealth` | `financialHealth < 50` | High | Financial health below 50 |
| `StrongFinancialHealth` | `financialHealth > 80` | Low | Strong financial health above 80 |
| `SlaBelowThreshold` | `slaUptime < 95` | High | SLA below 95% |
| `MajorIncidents` | `majorIncidents > 2` | High | More than 2 major incidents in the last 12 months |
| `SlowTicketResolution` | *never fires — no data* | Medium | Slow ticket resolution |
| `MissingIso27001` | `ISO27001` not held | High | Missing ISO27001 |
| `PrivacyPolicyExpired` | `privacyPolicyValid == false` | Medium | Privacy policy expired |
| `FailedPenTest` | `pentestReportValid == false` | Critical | Failed penetration test |

Boundaries follow the brief literally and are pinned by tests: `financialHealth` between 50 and 80 **inclusive** fires neither financial rule; an SLA of exactly 95 does not fire; exactly 2 major incidents does not fire. Certificates are stored upper-cased, and matching is case-insensitive regardless.

Section 5 calls the middle level *Moderate*; it is named `Medium` throughout so reason clauses and the overall `riskLevel` share one vocabulary of `Low` / `Medium` / `High` / `Critical`.

`StrongFinancialHealth` is a favourable finding. It appears in the reason for transparency but, being `Low`, never raises the overall level.

---

## API

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/vendor` | Register a vendor (409 if the name is taken) |
| `GET` | `/api/vendor?page=1&pageSize=20` | List vendors (paged) |
| `GET` | `/api/vendor/{id}` | Fetch one vendor |
| `PUT` | `/api/vendor/{id}` | Replace a vendor's inputs (409 if the name is taken; invalidates its cached assessment) |
| `DELETE` | `/api/vendor/{id}` | Remove a vendor |
| `GET` | `/api/vendor/{id}/risk` | Risk assessment with reason and breakdown |
| `GET` | `/api/vendor/compare?ids=1,2,3` | Side-by-side comparison (max 10) |
| `GET` | `/health` | Liveness plus PostgreSQL and Redis checks |

Errors come back as RFC 7807 problem documents. Validation rejects `financialHealth` and `slaUptime` outside 0–100, negative `majorIncidents`, and a name shorter than 2 or longer than 200 characters. All field errors are reported together rather than one at a time.

Two further rules apply to vendor data:

- **Vendor names are unique, irrespective of case and surrounding whitespace.** `POST` or `PUT` with a name another vendor holds returns **409 Conflict**; an update that keeps the vendor's own name is fine. The service checks before writing, and a unique index on `LOWER("Name")` is the real guard, so two concurrent requests cannot slip a duplicate through.
- **Security certificates are canonicalised** on the way in: trimmed, upper-cased, blanks dropped, and case-insensitive duplicates collapsed while preserving order. Posting `["iso27001","ISO27001"," Iso27001 ","soc2"]` stores `["ISO27001","SOC2"]`. Seeded rows go through the same normalisation.

### Example requests

**Create a vendor** — the payload from section 8 of the brief:

```bash
curl -X POST http://localhost:8080/api/vendor \
  -H 'Content-Type: application/json' \
  -d @data/example-create.json
```

```json
{
  "id": 16,
  "name": "TechPlus Solutions",
  "financialHealth": 78,
  "slaUptime": 93,
  "majorIncidents": 1,
  "securityCerts": [],
  "documents": { "contractValid": true, "privacyPolicyValid": false, "pentestReportValid": false }
}
```

**Assess it** — note that omitting `pentestReportValid` leaves it `false`, which the pentest rule reads as a failure:

```bash
curl http://localhost:8080/api/vendor/16/risk
```

```json
{
  "vendorId": 16,
  "vendorName": "TechPlus Solutions",
  "riskScore": 0,
  "riskLevel": "Critical",
  "reason": "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)",
  "dimensions": [
    { "category": "Financial", "riskLevel": "Low", "score": 0.0, "triggeredRules": [] },
    {
      "category": "Operational",
      "riskLevel": "High",
      "score": 0.0,
      "triggeredRules": [
        { "ruleId": "SlaBelowThreshold", "category": "Operational", "riskLevel": "High", "explanation": "SLA below 95%" }
      ]
    },
    {
      "category": "SecurityCompliance",
      "riskLevel": "Critical",
      "score": 0.0,
      "triggeredRules": [
        { "ruleId": "FailedPenTest", "category": "SecurityCompliance", "riskLevel": "Critical", "explanation": "Failed penetration test" },
        { "ruleId": "MissingIso27001", "category": "SecurityCompliance", "riskLevel": "High", "explanation": "Missing ISO27001" },
        { "ruleId": "PrivacyPolicyExpired", "category": "SecurityCompliance", "riskLevel": "Medium", "explanation": "Privacy policy expired" }
      ]
    }
  ],
  "triggeredRules": [ "... the same four findings as a flat list, most severe first ..." ],
  "evaluatedAtUtc": "2026-08-27T02:42:11.8673905Z"
}
```

**Compare vendors:**

```bash
curl 'http://localhost:8080/api/vendor/compare?ids=1,3,5'
```

---

## Dataset

| File | Contents |
| --- | --- |
| [`data/SampleVendorData.json`](data/SampleVendorData.json) | The 15 sample vendors from appendix B, seeded on first run |
| [`data/RiskFactorMatrix.json`](data/RiskFactorMatrix.json) | The full similarity matrix from appendix A — shipped but **not consumed** |
| [`data/example-create.json`](data/example-create.json) | The `POST /api/vendor` body from section 8 |

Seeding is idempotent: it runs only when the vendor table is empty, preserves the ids from the dataset, and realigns the identity sequence afterwards so later inserts do not collide.

How the seeded vendors assess under the current rule set:

| Level | Count | Vendors |
| --- | --- | --- |
| Critical | 10 | 2, 4, 5, 6, 8, 9, 11, 12, 13, 15 |
| High | 1 | 1 (TechPlus Solutions) |
| Low | 4 | 3, 7, 10, 14 |

Vendor 10 (HexaCloud DevOps) trips no rule at all and returns *"No significant risk factors identified (Low)"*. These expectations are pinned in [`SeedVendorAssessmentTests.cs`](tests/VendorRisk.UnitTests/Scoring/SeedVendorAssessmentTests.cs).

---

## Configuration

Settings come from `appsettings.json` and can be overridden by environment variables using `__` as the separator (e.g. `ConnectionStrings__Postgres`).

| Key | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Postgres` | `Host=localhost;…` | Required. The app fails fast at startup if absent. |
| `ConnectionStrings:Redis` | `localhost:6380` | Optional. Set it to an empty string to disable caching entirely (no-op cache). |
| `Database:MigrateOnStartup` | `true` | Set `false` when a pipeline applies migrations. |
| `Database:SeedDatasetPath` | `data/SampleVendorData.json` | Relative to the content root, or absolute. |

### Caching

Assessments are cached for 10 minutes under `vendor:{id}:assessment` and invalidated on update and delete. Cache faults are logged and swallowed — a Redis outage degrades to recomputation rather than failing the request.

### Logging

Serilog replaces the default logger and writes **compact JSON to stdout**, ready for shipping to ELK. Request logging and a machine-name enricher are on by default.

To run the full ELK stack:

```bash
docker compose -f docker-compose.yml -f docker-compose.elk.yml --profile elk up
```

That starts Elasticsearch and Kibana and adds the Elasticsearch sink to the API. The stack needs roughly 2 GB of RAM, which is why it sits behind a profile rather than in the default `up`.

Kibana lands on <http://localhost:5601>. A `kibana-setup` container registers the **VendorRisk logs** data view (`vendorrisk-logs-*`, time field `@timestamp`) once Kibana reports healthy, so **Discover works with no manual setup** — open Kibana, go to Discover, pick *VendorRisk logs*, and widen the time picker. The container checks whether the view exists before creating it, so restarts are harmless, and Elasticsearch keeps its data in the `elasticsearch-data` volume, so logs and saved objects survive `docker compose down`.

One quirk worth knowing when writing queries: the Serilog sink's index template names keyword sub-fields **`.raw`**, not the usual `.keyword`.

```
fields.RiskLevel.raw : "Critical"                 # works
fields.RiskLevel : "Critical"                     # matches nothing (analyzed text)
fields.VendorId : 5                               # mapped as long
fields.TriggeredRuleIds.raw : "FailedPenTest"     # every assessment that fired this rule
level.raw : "Error"                               # anything the exception middleware caught
```

Each assessment is logged with its vendor id, resulting level and the ids of the rules that fired, so the explainability the brief asks for is queryable in the log pipeline, not just in the API response.

### Migrations

Applied automatically at startup. To manage them by hand:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> \
  --project src/VendorRisk.Infrastructure \
  --startup-project src/VendorRisk.Api \
  --output-dir Persistence/Migrations
dotnet ef database update --project src/VendorRisk.Infrastructure --startup-project src/VendorRisk.Api
```

---

## Dashboard

The vendor comparison dashboard is served from the API root at <http://localhost:8080/>. It is a single dependency-free HTML page ([`wwwroot/index.html`](src/VendorRisk.Api/wwwroot/index.html)) that lists every vendor with its level and reason, and compares up to 10 side by side with their triggered findings. It follows the system light/dark theme.

The score column shows `0.00` throughout for the reason given in the Missing Code Notice.
