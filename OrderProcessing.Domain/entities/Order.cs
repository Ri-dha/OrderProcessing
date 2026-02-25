using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;

namespace OrderProcessing.Domain.entities;

public class Order : IEntity, IAuditableEntity, ISoftDeletable
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Draft] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.PaymentPending, OrderStatus.Cancelled,OrderStatus.PaymentFailed],
            [OrderStatus.PaymentPending] = [OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.Cancelled],
            [OrderStatus.Paid] = [OrderStatus.Fulfilling],
            [OrderStatus.Fulfilling] = [OrderStatus.Shipped],
            [OrderStatus.Shipped] = [OrderStatus.Delivered],
            [OrderStatus.Delivered] = [OrderStatus.RefundRequested],
            [OrderStatus.RefundRequested] = [OrderStatus.Refunded],
            [OrderStatus.PaymentFailed] = [OrderStatus.PaymentPending, OrderStatus.Cancelled],
            [OrderStatus.Cancelled] = [],
            [OrderStatus.Refunded] = []
        };

    public Guid Id { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public OrderStatus Status { get; private set; }
    
    public uint Version { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order()
    {
    }

    public static Order Create(IEnumerable<(Guid ProductId, int Quantity, decimal UnitPrice)> lines)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Draft
        };

        foreach (var line in lines)
        {
            order.AddItem(line.ProductId, line.Quantity, line.UnitPrice);
        }

        if (order._items.Count == 0)
        {
            throw new DomainValidationException("Cannot create an order without line items.");
        }

        return order;
    }

    public static IReadOnlyCollection<OrderStatus> AllowedFrom(OrderStatus current) =>
        AllowedTransitions.TryGetValue(current, out var transitions)
            ? transitions
            : [];

    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
        {
            ThrowTransitionError("add items");
        }

        if (quantity <= 0)
        {
            throw new DomainValidationException("Line item quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainValidationException("Line item price cannot be negative.");
        }

        _items.Add(OrderItem.Create(Id, productId, quantity, unitPrice));
    }

    public void TransitionTo(OrderStatus next)
    {
        var allowed = AllowedFrom(Status);
        if (!allowed.Contains(next))
        {
            ThrowTransitionError($"transition to {next}");
        }

        Status = next;
    }

    public void SetTrackingNumber(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            throw new DomainValidationException("Tracking number is required.");
        }

        TrackingNumber = trackingNumber.Trim();
    }

    public decimal TotalAmount() => _items.Sum(i => i.Quantity * i.UnitPrice);

    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;

    public void MarkAsDeleted() => IsDeleted = true;

    private void ThrowTransitionError(string action)
    {
        var allowed = AllowedFrom(Status);
        var allowedText = allowed.Count == 0 ? "none" : string.Join(", ", allowed);
        throw new DomainValidationException(
            $"Cannot {action}. Current status is {Status}. Allowed transitions from {Status}: {allowedText}.");
    }
}
