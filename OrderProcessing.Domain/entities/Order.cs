using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.enums;

namespace OrderProcessing.Domain.entities;


public class Order : IEntity, IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public DateTime CreatedAt { get; private set; } 
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; } = false;
    
    // Encapsulated collection
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Order() { }

    public static Order Create()
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;
    
    public void MarkAsDeleted() => IsDeleted = true;
    
    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Cannot add items. Current status is {Status}. Allowed from: {OrderStatus.Draft}");

        _items.Add(new OrderItem(Id, productId, quantity, unitPrice));
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Cannot confirm order. Current status is {Status}. Allowed from: {OrderStatus.Draft}");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot confirm an order with no items.");

        Status = OrderStatus.Confirmed;
    }

    public void MarkPaymentPending()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot process payment. Current status is {Status}. Allowed from: {OrderStatus.Confirmed}");

        PaymentStatus = PaymentStatus.PaymentPending;
    }

    public void MarkPaid()
    {
        if (PaymentStatus != PaymentStatus.PaymentPending)
            throw new InvalidOperationException($"Cannot mark as paid. Current status is {Status}. Allowed from: {PaymentStatus.PaymentPending}");

        PaymentStatus = PaymentStatus.Paid;
    }

    public void MarkPaymentFailed()
    {
        if (PaymentStatus != PaymentStatus.PaymentPending)
            throw new InvalidOperationException($"Cannot fail payment. Current status is {Status}. Allowed from: {PaymentStatus.PaymentPending}");

        PaymentStatus = PaymentStatus.PaymentFailed;
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
            throw new InvalidOperationException($"Cannot cancel order. Current status is {Status}. Allowed from: {OrderStatus.Draft}, {OrderStatus.Confirmed}");

        Status = OrderStatus.Cancelled;
    }
    
    // We will add the Fulfilling, Shipped, Delivered, and Refund methods later
}

