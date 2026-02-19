using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.repositories;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.Items) // Always load items with the order
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public void Add(Order order)
    {
        _context.Orders.Add(order);
    }
}