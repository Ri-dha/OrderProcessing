namespace OrderProcessing.Domain.entities;

public class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(string key, Guid orderId)
    {
        Id = Guid.NewGuid();
        Key = key;
        OrderId = orderId;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsCompleted => ResponseStatusCode.HasValue && ResponseBody is not null;

    public void Complete(int statusCode, string responseBody)
    {
        ResponseStatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAt = DateTime.UtcNow;
    }
}
