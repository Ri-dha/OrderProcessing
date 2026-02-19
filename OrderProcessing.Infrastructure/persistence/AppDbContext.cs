
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.entities;

namespace OrderProcessing.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryLog> InventoryLogs => Set<InventoryLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // This single line magically finds OrderConfiguration, ProductConfiguration, etc. 
        // and applies them to the database schema.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        // GLOBAL QUERY FILTERS: Automatically ignore deleted items in any query
        modelBuilder.Entity<Order>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
    }
    
    
    // THIS IS THE INTERFACE MAGIC
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            // 1. Auto-update the UpdatedAt date
            if (entry.Entity is IAuditableEntity auditable && entry.State == EntityState.Modified)
            {
                auditable.MarkAsUpdated();
            }

            // 2. Intercept Hard Deletes and turn them into Soft Deletes
            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified; // Change state to prevent hard delete
                softDeletable.MarkAsDeleted();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}