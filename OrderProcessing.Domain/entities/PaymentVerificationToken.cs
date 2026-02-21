namespace OrderProcessing.Domain.entities;

public class PaymentVerificationToken
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private PaymentVerificationToken()
    {
    }

    public static PaymentVerificationToken Create(Guid orderId, TimeSpan ttl)
    {
        return new PaymentVerificationToken
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl)
        };
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    public bool IsUsed() => UsedAt.HasValue;

    public void MarkUsed()
    {
        UsedAt = DateTime.UtcNow;
    }
}
