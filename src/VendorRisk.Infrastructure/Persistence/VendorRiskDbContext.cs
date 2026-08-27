using Microsoft.EntityFrameworkCore;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence;

public class VendorRiskDbContext : DbContext
{
    public VendorRiskDbContext(DbContextOptions<VendorRiskDbContext> options)
        : base(options)
    {
    }

    public DbSet<VendorProfile> Vendors => Set<VendorProfile>();

    /// <summary>Catalogue of certifications a vendor can hold.</summary>
    public DbSet<SecurityCertificate> Certificates => Set<SecurityCertificate>();

    /// <summary>The vendor-to-certificate join table, exposed for direct queries over the links.</summary>
    public DbSet<VendorCertificate> VendorCertificates => Set<VendorCertificate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VendorRiskDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
