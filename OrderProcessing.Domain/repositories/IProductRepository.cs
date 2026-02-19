using OrderProcessing.Domain.entities;

namespace OrderProcessing.Domain.repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(Product product);
}