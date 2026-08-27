# Vendor Risk Scoring Engine

A rule-based vendor risk scoring service built for the *Vendor Risk Scoring Engine (Rule-Based Edition)* case study. Every assessment is explainable: each score comes with the rules that produced it, a per-dimension breakdown, and a human-readable reason.

```http
GET /api/vendor/1/risk
```
```json
{
  "vendorId": 1,
  "vendorName": "TechPlus Solutions",
  "riskScore": 0.42,
  "riskLevel": "High",
  "reason": "SLA below 95% (High) + Privacy policy expired (Medium)"
}
```

> ### ⚠️ Missing Code Notice
>
> Per section 13 of the brief, the incomplete parts are documented here. The build succeeds, all tests pass, and every endpoint works — but:
>
> 1. **`SlowTicketResolutionRule` never fires.** Section 5 defines "Slow ticket resolution → Moderate risk", but neither the domain model nor the sample dataset carries a ticket-resolution measure. The rule is registered and tested, and returns `null` until such a field exists.
> 2. **"Failed penetration test" is inferred from `pentestReportValid`.** The dataset has no pass/fail result, only report validity, so an invalid or missing report is treated as a failed test. Because 10 of the 15 sample vendors have `pentestReportValid: false`, and that rule is Critical, **most of the seed data assesses as Critical**. This is the data, not a bug.
> 3. **`contractValid` has no rule.** Section 5 defines no condition for it, so none was invented. It is stored and returned by the API but never affects an assessment.
>
> `riskScore` and the appendix A matrix were both on this list in earlier revisions; both are now implemented — see [Scoring](#scoring), which also sets out the assumptions behind them.
>
> One further note: the brief's own worked example is internally inconsistent. TechPlus at `0.45 / 0.62 / 0.77` yields **0.597** under the section 7 formula, not the **0.67** shown in the table, and neither number is reproducible from the section 5 rules. This build calibrates to the formula rather than to that example.

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

146 tests covering every rule boundary, the graded impacts and baselines, the matrix propagation, the engine's roll-up and reason formatting, the certificate links and the shipped datasets, the service's cache and commit behaviour, controller status codes, and a regression theory pinning the level, reason and score of all 15 seeded vendors.

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

Every boundary is an interface — `IRiskRule`, `IRiskScoringEngine`, `IRiskFactorMatrix`, `IVendorRepository`, `ISecurityCertificateRepository`, `IUnitOfWork`, `ICacheService`, `IVendorService` — which is what lets the unit tests run with mocks and no database.

### Unit of work

`DbContext` is already a unit of work and `DbSet<T>` already a repository, so [`IUnitOfWork`](src/VendorRisk.Application/Abstractions/IUnitOfWork.cs) is deliberately thin: it exists so the application layer has a commit boundary it can call without referencing EF Core, and so that no repository saves behind the service's back.

**Repositories only stage work.** `Add`, `Update` and `Remove` are `void` — they touch the change tracker and nothing else. `VendorService` decides where the transaction ends and closes it with one `SaveChangesAsync`:

```csharp
vendor.SetCertificates(await _certificates.ResolveAsync(request.SecurityCerts, ct));
_repository.Add(vendor);
await _unitOfWork.SaveChangesAsync(ct);   // vendor + links + new catalogue rows, one transaction
```

That is not just tidier — it is a behaviour fix. Creating a vendor whose payload names an unknown certificate code used to be **two** commits, one in each repository, so a vendor write that failed left the catalogue row it had registered behind. Now the new row is staged and either both land or neither does.

Two further consequences, both deliberate:

- **Uniqueness violations are translated at the commit point.** The services check first so the common case answers cleanly, but two concurrent requests can both pass that check, and only the unique index actually stops them. [`UnitOfWork`](src/VendorRisk.Infrastructure/Persistence/UnitOfWork.cs) matches the violated constraint by name and raises `DuplicateVendorNameException` or `DuplicateCertificateCodeException`, which the middleware answers with **409** instead of a 500. Verified by racing six identical creates: one `201`, five `409`, no orphan rows.
- **The cache is invalidated after the commit, never before.** An eviction ahead of a write that then fails would discard a valid entry for nothing.

Transactions are left implicit: EF Core wraps every `SaveChanges` in one, and no operation here needs more than a single save, so `IUnitOfWork` exposes no explicit `BeginTransaction`. The seeder is the one place that still saves through the `DbContext` directly — it is startup work rather than a request, and it needs raw SQL for the identity sequence.

### How an assessment is produced

1. Every registered `IRiskRule` is evaluated against the vendor. A rule returns `null` when it does not fire, and an impact in `0..1` when it does.
2. **Reason** joins each finding as `Explanation (Level)` with ` + `, most severe first. Rules of equal severity keep DI registration order, so the string is deterministic.
3. Each dimension combines its findings with a graded baseline, then with the risks the similarity matrix implies — see [Scoring](#scoring).
4. **`riskScore`** weights the three dimensions `0.4 / 0.3 / 0.3`, exactly as section 7 defines.
5. **`riskLevel`** is the more severe of the highest triggered rule and the score's own band.

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

Errors come back as RFC 7807 problem documents. Besides the duplicate-name conflict below, a **409** also answers the rare case where two requests register the same previously unknown certificate code at once — the loser repeats the request and links to the row that won. Validation rejects `financialHealth` and `slaUptime` outside 0–100, negative `majorIncidents`, and a name shorter than 2 or longer than 200 characters. All field errors are reported together rather than one at a time.

Two further rules apply to vendor data:

- **Vendor names are unique, irrespective of case and surrounding whitespace.** `POST` or `PUT` with a name another vendor holds returns **409 Conflict**; an update that keeps the vendor's own name is fine. The service checks before writing, and a unique index on `LOWER("Name")` is the real guard, so two concurrent requests cannot slip a duplicate through.
- **Security certificates are catalogue rows, not free text.** `securityCerts` still carries plain codes in the JSON contract, but each code is resolved against the `certificates` table and linked to the vendor through `vendor_certificates` — see [Certificates](#certificates). Codes are canonicalised on the way in: trimmed, upper-cased, blanks dropped, and case-insensitive duplicates collapsed. Posting `["iso27001","ISO27001"," Iso27001 ","soc2"]` links `ISO27001` and `SOC2`. Seeded rows go through the same normalisation, and responses list the codes sorted so the payload never depends on join order.

### Example requests

**Create a vendor** — the payload from section 8 of the brief, renamed: vendor names are unique and appendix B already ships a *TechPlus Solutions*, so the brief's own body would answer **409** against a seeded database.

```bash
curl -X POST http://localhost:8080/api/vendor \
  -H 'Content-Type: application/json' \
  -d @data/example-create.json
```

```json
{
  "id": 16,
  "name": "TechPlus Solutions (2026 renewal)",
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
  "vendorName": "TechPlus Solutions (2026 renewal)",
  "riskScore": 0.57,
  "riskLevel": "Critical",
  "reason": "Failed penetration test (Critical) + SLA below 95% (High) + Missing ISO27001 (High) + Privacy policy expired (Medium)",
  "dimensions": [
    {
      "category": "Financial",
      "riskLevel": "Low",
      "score": 0.03,
      "baseline": { "value": 0.03, "basis": "Financial health 78 of 100" },
      "triggeredRules": [],
      "relatedRisks": []
    },
    {
      "category": "Operational",
      "riskLevel": "High",
      "score": 0.86,
      "baseline": { "value": 0.13, "basis": "1 major incident(s) in the last 12 months" },
      "triggeredRules": [
        { "ruleId": "SlaBelowThreshold", "category": "Operational", "riskLevel": "High", "explanation": "SLA below 95%" }
      ],
      "relatedRisks": [
        { "risk": "downtime", "similarity": 0.87, "impliedImpact": 0.66, "sourceRuleId": "SlaBelowThreshold" },
        { "risk": "slowTicketResolution", "similarity": 0.83, "impliedImpact": 0.63, "sourceRuleId": "SlaBelowThreshold" },
        { "risk": "serviceInstability", "similarity": 0.79, "impliedImpact": 0.6, "sourceRuleId": "SlaBelowThreshold" }
      ]
    },
    {
      "category": "SecurityCompliance",
      "riskLevel": "Critical",
      "score": 1,
      "baseline": { "value": 0, "basis": "No graded inputs: certificates and documents are either held or not" },
      "triggeredRules": [
        { "ruleId": "FailedPenTest", "category": "SecurityCompliance", "riskLevel": "Critical", "explanation": "Failed penetration test" },
        { "ruleId": "MissingIso27001", "category": "SecurityCompliance", "riskLevel": "High", "explanation": "Missing ISO27001" },
        { "ruleId": "PrivacyPolicyExpired", "category": "SecurityCompliance", "riskLevel": "Medium", "explanation": "Privacy policy expired" }
      ],
      "relatedRisks": [
        { "risk": "internalVulnerabilities", "similarity": 0.88, "impliedImpact": 0.88, "sourceRuleId": "FailedPenTest" },
        { "risk": "weakAccessControl", "similarity": 0.84, "impliedImpact": 0.59, "sourceRuleId": "MissingIso27001" },
        { "risk": "missingNDA", "similarity": 0.81, "impliedImpact": 0.32, "sourceRuleId": "PrivacyPolicyExpired" }
      ]
    }
  ],
  "triggeredRules": [ "... the same four findings as a flat list, most severe first ..." ],
  "evaluatedAtUtc": "2026-08-27T10:52:21.8081372Z"
}
```

`relatedRisks` is abbreviated here: the security dimension actually returns all nine implications, three per finding. Note too that its score is already `1` from the Critical finding alone, so those implications change nothing — see [Scoring](#scoring).

**Compare vendors:**

```bash
curl 'http://localhost:8080/api/vendor/compare?ids=1,3,5'
```

---

## Scoring

The brief fixes the category weights in section 7 and nothing else: no per-rule weights, no way to combine several findings, no score-to-level thresholds, and no method for turning appendix A's *similarity* coefficients into a number — and its own worked example is not reproducible from its own rules. So **the weights below are the brief's; everything else is an assumption, recorded here.** Every constant lives in [`RiskWeights.cs`](src/VendorRisk.Domain/Risk/RiskWeights.cs).

```
riskScore = 0.4 × Financial + 0.3 × Operational + 0.3 × SecurityCompliance      (section 7)
```

### 1. What a finding is worth

Severity sets the base impact — `Low 0.10 / Medium 0.40 / High 0.70 / Critical 1.00` — and a rule with a continuous input is then graded **inside its own band**, so a vendor barely past a threshold does not score like one far past it:

| Rule | Impact | At the edges |
| --- | --- | --- |
| `LowFinancialHealth` | `0.70 + 0.30 × (50 — health)/50` | 49 → 0.71, 0 → 1.00 |
| `SlaBelowThreshold` | `0.70 + 0.30 × clamp((95 — sla)/10)` | 94.9 → 0.70, 85 and below → 1.00 |
| `MajorIncidents` | `0.70 + 0.30 × clamp((n — 2)/3)` | 3 → 0.80, 5 and above → 1.00 |
| `MissingIso27001`, `PrivacyPolicyExpired`, `FailedPenTest` | flat `0.70` / `0.40` / `1.00` | binary inputs, nothing to grade |
| `StrongFinancialHealth` | `0.00` | favourable: it appears in the reason, it adds no risk |

### 2. The graded baseline — the main assumption

Section 5 defines cliffs, not curves, and leaves gaps between them. The largest is financial: nothing at all is said about health between 50 and 80, in the category section 7 weights **the heaviest**. Left strictly to the rules, that 0.4-weighted dimension would read `0.00` for 13 of the 15 sample vendors and the scores would bunch together.

So each category grades its continuous inputs across their whole range, anchored on section 5's own thresholds and capped at `0.40` — the Medium impact — so a baseline can never outweigh a real finding:

| Category | Baseline | Reasoning |
| --- | --- | --- |
| Financial | `0.40 × clamp((80 — health)/30)` | 0 at the "strong" threshold, the cap at the "high risk" one |
| Operational | `0.40 × clamp(min(n,3)/3)` | Incidents under section 5's "more than 2" bar are still evidence |
| SecurityCompliance | `0.00` | Every input is binary — a certificate is held or not, a document is valid or not |

Baselines carry the sentence that justifies them (`"Financial health 75 of 100"`), so no number in a payload is left unexplained. They are **not** rules: the rule set stays exactly as section 5 writes it, and no reason string is invented.

### 3. Combining findings within a category

```
observed = 1 — product of (1 — impact)      over the findings and the baseline
```

Contributions are treated as independent, so each closes a share of the remaining distance to 1. Order-independent, monotonic — more findings always mean more risk — and bounded: two Mediums (0.64) outrank either alone but stay under a High. A Critical finding contributes 1 and saturates its category outright, which is what "Critical" should mean.

### 4. The similarity matrix

Section 2.3 says to *"compute a score using the Risk Similarity Matrix"*, and section 6 frames it as *risk item to similar risks*. Read literally: **an observed finding implies the risks that tend to come with it.** Each rule declares the matrix entry it observes, and each neighbour is implied at `impact × similarity`:

```
category = observed + (1 — observed) × 0.5 × strongestImpliedRisk
```

An implied risk is inferred rather than seen, so it counts half, and it can only close part of the gap to 1 — never push past it. Where two findings imply the same risk the stronger wins; a risk the dimension already observes is not implied on top of itself; and a saturated category has no room left, so a Critical finding's implications change nothing.

An SLA of 90% (impact 0.85) implies `downtime 0.87 → 0.74`, `slowTicketResolution 0.83 → 0.71` and `serviceInstability 0.79 → 0.67`. The strongest lifts that dimension from `0.85` to `0.91`. All of them are returned in `relatedRisks`, because they are part of the picture even though only one moves the number.

The matrix is read once at startup from [`data/RiskFactorMatrix.json`](data/RiskFactorMatrix.json) into a singleton, [`JsonRiskFactorMatrix`](src/VendorRisk.Infrastructure/Scoring/JsonRiskFactorMatrix.cs). Its four groups are containers only — node names are unique across them — and most neighbours it names have no entry of their own, which is expected: scoring needs a coefficient, not a row. **If the file is missing or malformed the API logs a warning and scores on observed findings alone** rather than failing to start.

Six of the seven firing rules map onto a matrix entry directly. `LowFinancialHealth` has no exact counterpart — the matrix describes `lowCashFlow`, `highDebtRatio` and `creditDowngrade`, not a health score — and is mapped to `lowCashFlow` as the general financial-distress entry. That is an assumption.

### 5. Score to level

Bands: `< 0.25 Low`, `< 0.50 Medium`, `< 0.75 High`, `≥ 0.75 Critical`.

`riskLevel` is **the more severe of** the highest triggered rule and the band. The score can raise a level — several Medium findings together are worse than any one alone — but never lower it: a failed penetration test on an otherwise sound vendor scores only `0.30` overall, and must still read as Critical. On the sample data the rule reading wins every time, so **no vendor's level changed when the score landed**; what changed is that the ten vendors sharing `Critical` can now be ranked.

### Computed, never stored

There is no assessment table. `riskScore`, `riskLevel`, `reason` and the whole breakdown are derived from the vendor's current data on every request, so a score cannot drift out of step with the vendor it describes, and changing a weight or a rule changes every answer immediately rather than only for vendors written afterwards.

The one thing that is kept is the **finished response**, cached in Redis under `vendor:{id}:assessment` for 10 minutes and dropped the moment the vendor is updated or deleted. A cache hit returns exactly what a recomputation would have produced from the same data; a miss, a cold start and a Redis outage all just mean scoring it again. Both `/api/vendor/{id}/risk` and `/api/vendor/compare` read through that same cache, so comparing vendors reuses whatever has already been computed for them.

### Worked example — TechPlus Solutions (vendor 1)

`financialHealth 78, slaUptime 93, 1 incident, ISO27001 held, privacy policy invalid, pentest valid`

| Dimension | Findings | Baseline | Strongest implied | Score |
| --- | --- | --- | --- | --- |
| Financial | — | 0.03 (health 78) | — | **0.03** |
| Operational | SLA below 95% = 0.76 | 0.13 (1 incident) | downtime 0.66 | **0.86** |
| SecurityCompliance | Privacy policy expired = 0.40 | — | missingNDA 0.32 | **0.50** |

`0.4 × 0.03 + 0.3 × 0.86 + 0.3 × 0.50 = 0.42`. Band says Medium, the rules say High, so the assessment is **High**.

### Calibration over the sample data

| Score | Vendor | Score | Vendor |
| --- | --- | --- | --- |
| 0.96 | DataBridge Analytics | 0.66 | BlueWave Consulting |
| 0.95 | TrustCom IT Services | 0.55 | PrimeNet Security |
| 0.75 | VisionTech Support | 0.42 | TechPlus Solutions |
| 0.73 | NovaLog Logistics | 0.39 | GlobalTrans Freight |
| 0.68 | AlphaCloud Hosting | 0.03 | HexaCloud DevOps |
| 0.68 | CargoLine Transport | 0.00 | SecurePay, Skyline, Orion |
| 0.67 | Velocity Warehousing | | |

Each of these is pinned per vendor in [`SeedVendorAssessmentTests.cs`](tests/VendorRisk.UnitTests/Scoring/SeedVendorAssessmentTests.cs): changing a weight, a baseline or the damping factor moves them, and that should never happen quietly.

---

## Certificates

A certification is a thing in its own right — it has a code, a full name and a description, and many vendors hold the same one — so it lives in its own table rather than in an array column on the vendor.

```
vendors  ──<  vendor_certificates  >──  certificates
  Id           VendorId  (FK, cascade)     Id
  Name         CertificateId (FK, restrict) Code    unique, upper-cased
  …            PK (VendorId, CertificateId) Name
                                            Description
```

- **`certificates`** is the shared catalogue, unique by `Code`. [`data/SecurityCertificates.json`](data/SecurityCertificates.json) seeds ISO27001, ISO22301, SOC2 and PCI-DSS with their full names.
- **`vendor_certificates`** is the join table, keyed on the pair, so the same certificate cannot be linked to a vendor twice. It is mapped from an explicit `VendorCertificate` entity rather than EF's implicit join type, so the table can be named and queried like any other.
- Deleting a **vendor** cascades to its links and leaves the catalogue untouched. Deleting a **certificate** still held by a vendor is refused (`Restrict`).
- A `POST` or `PUT` naming a code the catalogue does not hold **registers it** rather than rejecting the request, since the case study's contract takes free-form codes. Its name defaults to the code until someone gives it a better one.
- `VendorProfile.SecurityCerts` is a sorted projection over the linked codes, which is what keeps the section 4 JSON shape unchanged; `HasCertification` — the lookup `MissingIso27001Rule` uses — remains case-insensitive.

The `AddCertificateTables` migration carries existing data across: the codes in the old `text[]` column become catalogue rows and links before the column is dropped, and its `Down` writes them back, so the change is reversible on a populated database.

---

## Dataset

| File | Contents |
| --- | --- |
| [`data/SampleVendorData.json`](data/SampleVendorData.json) | The 15 sample vendors from appendix B, seeded on first run |
| [`data/SecurityCertificates.json`](data/SecurityCertificates.json) | The certificate catalogue — codes with their full names. Not from the brief, which names the certifications (section 2) but ships no catalogue |
| [`data/RiskFactorMatrix.json`](data/RiskFactorMatrix.json) | The full similarity matrix from appendix A, 15 entries, read at startup — see [Scoring](#scoring) |
| [`data/example-create.json`](data/example-create.json) | The `POST /api/vendor` body from section 8 |

Seeding is idempotent and per table: the catalogue is filled only while `certificates` is empty and the vendors only while `vendors` is empty. Vendor ids from the dataset are preserved and the identity sequence is realigned afterwards so later inserts do not collide. A code used by a sample vendor that the catalogue does not describe is registered on the spot and logged as a warning — a test pins the two files against each other so that should not happen.

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
| `Database:SeedCertificateCatalogPath` | `data/SecurityCertificates.json` | The certificate catalogue. Relative to the content root, or absolute. |
| `Scoring:RiskFactorMatrixPath` | `data/RiskFactorMatrix.json` | The similarity matrix. A missing file degrades scoring to observed findings; it does not fail startup. |

### Caching

Assessments are cached for 10 minutes under `vendor:{id}:assessment` and invalidated on update and delete. Both the single-assessment and the comparison endpoint read through it. Cache faults are logged and swallowed — a Redis outage degrades to recomputation rather than failing the request, because nothing is ever read from the cache that could not be recomputed. See [Computed, never stored](#computed-never-stored).

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

The score column is what ranks vendors that share a risk level — ten of the fifteen sample vendors are `Critical`, and the score is what separates them.
