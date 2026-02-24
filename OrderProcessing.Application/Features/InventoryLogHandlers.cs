using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;
using Wolverine.Attributes;

namespace OrderProcessing.Application.Features;

public class InventoryLogHandlers
{
    [WolverineHandler]
    public void Handle(StockReservedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Reservation, @event.Quantity));
    }

    [WolverineHandler]
    public void Handle(StockReleasedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Release, @event.Quantity));
    }

    [WolverineHandler]
    public void Handle(StockRestockedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Restock, @event.Quantity));
    }

    [WolverineHandler]
    public void Handle(FulfillmentCommittedEvent @event, AppDbContext db)
    {
        db.InventoryLogs.Add(new InventoryLog(@event.ProductId, @event.OrderId, InventoryLogType.Deduction, @event.Quantity));
    }
}
