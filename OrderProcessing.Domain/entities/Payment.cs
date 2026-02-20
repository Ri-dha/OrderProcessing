using OrderProcessing.Domain.enums;

namespace OrderProcessing.Domain.entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    private Payment()
    {
    }

    public static Payment Create(Guid orderId, decimal amount, PaymentStatus status)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRefunded()
    {
        Status = PaymentStatus.Refunded;
        RefundedAt = DateTime.UtcNow;
    }
}
