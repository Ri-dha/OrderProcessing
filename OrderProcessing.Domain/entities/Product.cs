using OrderProcessing.Domain.Common;

namespace OrderProcessing.Domain.entities;


public class Product: IEntity, IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    
    // We will use EF Core concurrency tokens on this field later to prevent overselling
    public long AvailableStock { get; private set; }
    
    public uint Version { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Product() { }

    public Product(string name, string sku, decimal price, int initialStock)
    {
        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Price = price;
        AvailableStock = initialStock;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;
    public void MarkAsDeleted() => IsDeleted = true;

    public void ReserveStock(int quantity)
    {
        if (AvailableStock < quantity)
            throw new InvalidOperationException($"Insufficient stock for product {Sku}. Requested: {quantity}, Available: {AvailableStock}");

        AvailableStock -= quantity;
    }

    public void ReleaseStock(int quantity)
    {
        AvailableStock += quantity;
    }
}