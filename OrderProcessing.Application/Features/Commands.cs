namespace OrderProcessing.Application.Features;

public sealed record CreateProductCommand(string Name, string Sku, decimal Price, int InitialStock);
public sealed record CreateProductsBulkCommand(IReadOnlyList<CreateProductCommand> Products);
public sealed record UpdateProductCommand(Guid ProductId, string? Name, string? Sku, decimal? Price, int? Stock, bool? IsDeleted);

public sealed record CreateOrderCommand(IReadOnlyList<CreateOrderItemCommand> Items);

public sealed record CreateOrderItemCommand(Guid ProductId, int Quantity);

public sealed record ConfirmOrderCommand(Guid OrderId);

public sealed record CancelOrderCommand(Guid OrderId);

public sealed record InitiatePaymentCommand(Guid OrderId, string CardNumber, string ExpiryDate, string Cvc);

public sealed record VerifyPaymentCommand(Guid OrderId, string VerificationToken, string IdempotencyKey);
public sealed record ProcessPaymentVerificationCommand(Guid OrderId, string VerificationToken, string IdempotencyKey);

public sealed record StartFulfillmentCommand(Guid OrderId);

public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber);

public sealed record DeliverOrderCommand(Guid OrderId);

public sealed record RequestRefundCommand(Guid OrderId);

public sealed record CompleteRefundCommand(Guid OrderId);

public sealed record CleanupIdempotencyRecordsCommand;

public sealed record GetProductsQuery(int? Page, int? PageSize);
public sealed record GetOrdersQuery(int? Page, int? PageSize);
public sealed record GetOrderByIdQuery(Guid OrderId);
public sealed record GetOrderInventoryLogsQuery(Guid OrderId);
public sealed record PollPaymentVerificationStatusQuery(Guid OrderId, string IdempotencyKey);

public record StockReservedEvent(Guid ProductId, Guid OrderId, int Quantity);
public record StockReleasedEvent(Guid ProductId, Guid OrderId, int Quantity);
public record StockRestockedEvent(Guid ProductId, Guid OrderId, int Quantity);
public record FulfillmentCommittedEvent(Guid ProductId, Guid OrderId, int Quantity);
