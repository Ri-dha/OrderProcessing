using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.entities;


namespace OrderProcessing.Infrastructure.Persistence;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);
               
        builder.HasIndex(p => p.Sku).IsUnique();
        
        // THE CONCURRENCY MAGIC
        // This tells EF Core to use PostgreSQL's internal xmin column to prevent overselling.
        builder.Property(p => p.Version)
            .IsRowVersion();
    }
}