using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Application.Features;

public class InventoryLogHandlers
{
    public void Handle(StockReservedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Reservation, @event.Quantity));
    }

    public void Handle(StockReleasedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Release, @event.Quantity));
    }

    public void Handle(StockRestockedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Restock, @event.Quantity));
    }

    public void Handle(StockDeductedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Deduction, @event.Quantity));
    }
}
