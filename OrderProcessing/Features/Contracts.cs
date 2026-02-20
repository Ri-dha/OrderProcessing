namespace OrderProcessing.Features;

public sealed record CreateProductRequest(string Name, string Sku, decimal Price, int InitialStock);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);

public sealed record CreateOrderRequest(IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record ShipOrderRequest(string TrackingNumber);

public sealed record OperationResponse(string Message, Guid OrderId, string Status);

public sealed record PaymentResponse(Guid OrderId, string Status, decimal Amount, string Message);

public sealed record CreatedResponse(Guid Id, string Message);

public sealed record OrderDetailsResponse(
    Guid Id,
    string Status,
    string? TrackingNumber,
    decimal TotalAmount,
    IReadOnlyList<OrderLineResponse> Items);

public sealed record OrderLineResponse(Guid ProductId, int Quantity, decimal UnitPrice);
