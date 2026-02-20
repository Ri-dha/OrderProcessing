namespace OrderProcessing.Features;

public sealed record CreateProductCommand(string Name, string Sku, decimal Price, int InitialStock);

public sealed record CreateOrderCommand(IReadOnlyList<CreateOrderItemCommand> Items);

public sealed record CreateOrderItemCommand(Guid ProductId, int Quantity);

public sealed record ConfirmOrderCommand(Guid OrderId);

public sealed record CancelOrderCommand(Guid OrderId);

public sealed record ProcessPaymentCommand(Guid OrderId, string IdempotencyKey);

public sealed record StartFulfillmentCommand(Guid OrderId);

public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber);

public sealed record DeliverOrderCommand(Guid OrderId);

public sealed record RequestRefundCommand(Guid OrderId);

public sealed record CompleteRefundCommand(Guid OrderId);

public sealed record CleanupIdempotencyRecordsCommand;
