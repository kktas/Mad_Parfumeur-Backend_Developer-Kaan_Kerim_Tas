using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence.Configurations;

public sealed class VendorProfileConfiguration : IEntityTypeConfiguration<VendorProfile>
{
    public void Configure(EntityTypeBuilder<VendorProfile> builder)
    {
        builder.ToTable("vendors");

        builder.HasKey(vendor => vendor.Id);

        builder.Property(vendor => vendor.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(vendor => vendor.FinancialHealth).IsRequired();

        // Percentages need one decimal place at most; numeric(5,2) covers 0.00 - 100.00.
        builder.Property(vendor => vendor.SlaUptime)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(vendor => vendor.MajorIncidents).IsRequired();

        // Npgsql maps List<string> to text[], which keeps the certificates queryable.
        builder.Property(vendor => vendor.SecurityCerts)
            .HasColumnType("text[]")
            .IsRequired();

        builder.OwnsOne(vendor => vendor.Documents, documents =>
        {
            documents.Property(document => document.ContractValid)
                .HasColumnName("contract_valid")
                .IsRequired();

            documents.Property(document => document.PrivacyPolicyValid)
                .HasColumnName("privacy_policy_valid")
                .IsRequired();

            documents.Property(document => document.PentestReportValid)
                .HasColumnName("pentest_report_valid")
                .IsRequired();
        });

        builder.Navigation(vendor => vendor.Documents).IsRequired();

        builder.Property(vendor => vendor.CreatedAtUtc).IsRequired();
        builder.Property(vendor => vendor.UpdatedAtUtc).IsRequired();

        builder.HasIndex(vendor => vendor.Name);
    }
}
