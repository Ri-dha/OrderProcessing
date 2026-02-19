using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.repositories;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Products.FindAsync(new object[] { id }, cancellationToken);
    }

    public void Add(Product product)
    {
        _context.Products.Add(product);
    }
}