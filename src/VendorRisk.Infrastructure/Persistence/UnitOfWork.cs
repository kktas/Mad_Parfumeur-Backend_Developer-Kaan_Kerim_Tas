using Microsoft.EntityFrameworkCore;
using Npgsql;
using VendorRisk.Application.Abstractions;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence;

/// <summary>
/// Commits the work staged on the request's DbContext. EF Core wraps every SaveChanges in a
/// transaction, so a vendor, its certificate links and any catalogue rows registered along the way
/// are written together or not at all.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    // The unique indexes the translation below keys on, both created by migrations. Matching the
    // constraint name is what tells one violation from another; the message text is not stable.
    private const string VendorNameIndex = "IX_vendors_Name_lower";
    private const string CertificateCodeIndex = "IX_certificates_Code";

    private readonly VendorRiskDbContext _dbContext;

    public UnitOfWork(VendorRiskDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Saving is also where uniqueness is really enforced: the services check first to answer
    /// cleanly, but two concurrent requests can both pass that check, and only the index stops
    /// them. Those violations become domain exceptions here so the API can answer 409 rather than
    /// surfacing an EF Core failure as a 500.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryFindOffender<VendorProfile>(ex, VendorNameIndex, out var vendor))
        {
            throw new DuplicateVendorNameException(vendor.Name);
        }
        catch (DbUpdateException ex) when (TryFindOffender<SecurityCertificate>(ex, CertificateCodeIndex, out var certificate))
        {
            throw new DuplicateCertificateCodeException(certificate.Code);
        }
    }

    /// <summary>
    /// Whether the failure is a unique violation on the named index, and if so which entity in the
    /// failed batch caused it. Returning false leaves the original exception to propagate untouched.
    /// </summary>
    private static bool TryFindOffender<TEntity>(DbUpdateException exception, string indexName, out TEntity entity)
        where TEntity : class
    {
        entity = null!;

        if (exception.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            || violation.ConstraintName != indexName)
        {
            return false;
        }

        entity = exception.Entries
            .Select(entry => entry.Entity)
            .OfType<TEntity>()
            .FirstOrDefault()!;

        return entity is not null;
    }
}
