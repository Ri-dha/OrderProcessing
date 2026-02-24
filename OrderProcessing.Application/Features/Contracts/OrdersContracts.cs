using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Application.Features.Contracts;

public sealed class CreateOrderItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class CreateOrderRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];
}

public sealed class ShipOrderRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string TrackingNumber { get; init; } = string.Empty;
}

public sealed record OrderDetailsResponse(
    Guid Id,
    string Status,
    string? TrackingNumber,
    decimal TotalAmount,
    IReadOnlyList<OrderLineResponse> Items);

public sealed record OrderSummaryResponse(
    Guid Id,
    string Status,
    string? TrackingNumber,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

public sealed record OrderLineResponse(Guid ProductId, int Quantity, decimal UnitPrice);
