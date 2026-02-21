using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.errors;

namespace OrderProcessing.Domain.entities;

public class Product : IEntity, IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int AvailableStock { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Product()
    {
    }

    public Product(string name, string sku, decimal price, int initialStock)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Product name is required.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainValidationException("Product SKU is required.");
        }

        if (price < 0)
        {
            throw new DomainValidationException("Product price cannot be negative.");
        }

        if (initialStock < 0)
        {
            throw new DomainValidationException("Initial stock cannot be negative.");
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Sku = sku.Trim();
        Price = price;
        AvailableStock = initialStock;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Product name is required.");
        }

        Name = name.Trim();
    }

    public void UpdateSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainValidationException("Product SKU is required.");
        }

        Sku = sku.Trim();
    }

    public void UpdatePrice(decimal price)
    {
        if (price < 0)
        {
            throw new DomainValidationException("Product price cannot be negative.");
        }

        Price = price;
    }

    public void UpdateStock(int stock)
    {
        if (stock < 0)
        {
            throw new DomainValidationException("Product stock cannot be negative.");
        }

        AvailableStock = stock;
    }

    public void SetDeleted(bool isDeleted)
    {
        IsDeleted = isDeleted;
    }

    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainValidationException("Reservation quantity must be greater than zero.");
        }

        if (AvailableStock < quantity)
        {
            throw new DomainValidationException(
                $"Insufficient stock for product {Sku}. Requested: {quantity}, Available: {AvailableStock}.");
        }

        AvailableStock -= quantity;
    }

    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainValidationException("Release quantity must be greater than zero.");
        }

        AvailableStock += quantity;
    }

    public void Restock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainValidationException("Restock quantity must be greater than zero.");
        }

        AvailableStock += quantity;
    }

    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;

    public void MarkAsDeleted() => IsDeleted = true;
}
