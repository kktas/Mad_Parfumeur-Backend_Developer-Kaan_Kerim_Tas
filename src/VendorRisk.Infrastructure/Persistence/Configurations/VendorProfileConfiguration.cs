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

        // Certificates are a shared catalogue, joined many-to-many through vendor_certificates.
        // The join entity is explicit so the table can be queried and named like the others.
        builder.HasMany(vendor => vendor.Certificates)
            .WithMany(certificate => certificate.Vendors)
            .UsingEntity<VendorCertificate>(
                join => join
                    .HasOne(vendorCertificate => vendorCertificate.Certificate)
                    .WithMany()
                    .HasForeignKey(vendorCertificate => vendorCertificate.CertificateId)
                    // A certificate still held by a vendor cannot be deleted out from under it.
                    .OnDelete(DeleteBehavior.Restrict),
                join => join
                    .HasOne(vendorCertificate => vendorCertificate.Vendor)
                    .WithMany()
                    .HasForeignKey(vendorCertificate => vendorCertificate.VendorId)
                    // Deleting a vendor drops its links, never the catalogue entries.
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("vendor_certificates");
                    join.HasKey(vendorCertificate =>
                        new { vendorCertificate.VendorId, vendorCertificate.CertificateId });

                    // The composite key covers vendor -> certificates; this covers the reverse.
                    join.HasIndex(vendorCertificate => vendorCertificate.CertificateId);
                });

        // Projection over Certificates for the API contract, not a column.
        builder.Ignore(vendor => vendor.SecurityCerts);

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

        // Vendor names are unique irrespective of case. EF cannot express an index over an
        // expression, so the unique index on lower("Name") is created by raw SQL in the
        // AddVendorNameUniqueIndex migration; this keeps the plain lookup index off the table.
    }
}
