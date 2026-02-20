using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.entities;

namespace OrderProcessing.Infrastructure.Persistence;

public class InventoryLogConfiguration : IEntityTypeConfiguration<InventoryLog>
{
    public void Configure(EntityTypeBuilder<InventoryLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(x => new { x.ProductId, x.Timestamp });
    }
}
