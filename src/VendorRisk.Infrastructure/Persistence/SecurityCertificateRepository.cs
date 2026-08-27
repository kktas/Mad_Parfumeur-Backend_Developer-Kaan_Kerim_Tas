using Microsoft.EntityFrameworkCore;
using Npgsql;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence;

public sealed class SecurityCertificateRepository : ISecurityCertificateRepository
{
    private readonly VendorRiskDbContext _dbContext;

    public SecurityCertificateRepository(VendorRiskDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<SecurityCertificate>> ResolveAsync(
        IEnumerable<string>? codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = SecurityCertificates.Normalise(codes);
        if (wanted.Count == 0)
        {
            return [];
        }

        var byCode = await LoadByCodeAsync(wanted, cancellationToken);

        // The API contract in section 4 takes free-form codes, so a code the catalogue has not seen
        // before is registered rather than rejected. Its display name defaults to the code itself
        // until someone gives it a better one; the seeded entries carry real names.
        var missing = wanted
            .Where(code => !byCode.ContainsKey(code))
            .Select(code => new SecurityCertificate { Code = code, Name = code })
            .ToList();

        if (missing.Count > 0)
        {
            _dbContext.Certificates.AddRange(missing);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                foreach (var certificate in missing)
                {
                    byCode[certificate.Code] = certificate;
                }
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Another request registered the same code first. Its row is the winner; drop ours
                // and read the catalogue again.
                foreach (var certificate in missing)
                {
                    _dbContext.Entry(certificate).State = EntityState.Detached;
                }

                byCode = await LoadByCodeAsync(wanted, cancellationToken);
            }
        }

        return [.. wanted.Where(byCode.ContainsKey).Select(code => byCode[code])];
    }

    private async Task<Dictionary<string, SecurityCertificate>> LoadByCodeAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Certificates
            .Where(certificate => codes.Contains(certificate.Code))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(certificate => certificate.Code, StringComparer.Ordinal);
    }
}
