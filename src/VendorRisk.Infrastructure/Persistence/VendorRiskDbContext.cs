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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VendorRiskDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
