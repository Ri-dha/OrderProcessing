using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.entities;

namespace OrderProcessing.Infrastructure.Persistence;

public class PaymentVerificationTokenConfiguration : IEntityTypeConfiguration<PaymentVerificationToken>
{
    public void Configure(EntityTypeBuilder<PaymentVerificationToken> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.HasIndex(x => new { x.OrderId, x.ExpiresAt });
    }
}
