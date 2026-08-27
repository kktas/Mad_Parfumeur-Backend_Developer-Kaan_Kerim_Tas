using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Infrastructure.Persistence.Configurations;

public sealed class SecurityCertificateConfiguration : IEntityTypeConfiguration<SecurityCertificate>
{
    public void Configure(EntityTypeBuilder<SecurityCertificate> builder)
    {
        builder.ToTable("certificates");

        builder.HasKey(certificate => certificate.Id);

        builder.Property(certificate => certificate.Code)
            .HasMaxLength(50)
            .IsRequired();

        // Codes are stored upper-cased, so an ordinary unique index is enough to keep the
        // catalogue free of duplicates; see SecurityCertificates.Normalise.
        builder.HasIndex(certificate => certificate.Code).IsUnique();

        builder.Property(certificate => certificate.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(certificate => certificate.Description)
            .HasMaxLength(500);
    }
}
