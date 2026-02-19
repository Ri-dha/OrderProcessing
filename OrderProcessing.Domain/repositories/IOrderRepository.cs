using OrderProcessing.Domain.entities;

namespace OrderProcessing.Domain.repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(Order order);
}