using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Application.Features;

public class InventoryLogHandler
{
    
    public async Task Handle(StockReservedEvent @event, AppDbContext db, CancellationToken ct)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Reservation, @event.Quantity));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(StockReleasedEvent @event, AppDbContext db, CancellationToken ct)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Release, @event.Quantity));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(StockRestockedEvent @event, AppDbContext db, CancellationToken ct)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Restock, @event.Quantity));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(FulfillmentCommittedEvent @event, AppDbContext db, CancellationToken ct)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Deduction, @event.Quantity));
        await db.SaveChangesAsync(ct);
    }
}