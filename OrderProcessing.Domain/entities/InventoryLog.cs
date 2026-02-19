using OrderProcessing.Domain.enums;

namespace OrderProcessing.Domain.entities;


public class InventoryLog
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid OrderId { get; private set; }
    public InventoryLogType Type { get; private set; }
    public int Quantity { get; private set; }
    public DateTime Timestamp { get; private set; }

    private InventoryLog() { }

    public InventoryLog(Guid productId, Guid orderId, InventoryLogType type, int quantity)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        OrderId = orderId;
        Type = type;
        Quantity = quantity;
        Timestamp = DateTime.UtcNow;
    }
}