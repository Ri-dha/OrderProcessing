namespace OrderProcessing.Application.Features;

public sealed record OperationResponse(string Message, Guid OrderId, string Status);

public sealed record PaymentResponse(Guid OrderId, string Status, decimal Amount, string Message);
public sealed record PaymentInitiationResponse(Guid OrderId, string Status, string VerificationToken, DateTime ExpiresAt, string Message);
public sealed record VerifyPaymentResult(int StatusCode, PaymentResponse? Response, string? Message = null);

public sealed record CreatedResponse(Guid Id, string Message);
public sealed record BulkCreatedResponse(int Count, IReadOnlyList<Guid> Ids, string Message);
public sealed record ProductResponse(Guid Id, string Name, string Sku, decimal Price, int AvailableStock, bool IsDeleted);
