using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.errors;
using Wolverine;
using Wolverine.Http;

namespace OrderProcessing.Features.Endpoints;

public static class OrdersEndpoints
{
    [WolverinePost("/api/orders")]
    public static async Task<IResult> CreateOrder(CreateOrderRequest request, IMessageBus bus, CancellationToken ct)
    {
        try
        {
            var cmd = new CreateOrderCommand(request.Items
                .Select(x => new CreateOrderItemCommand(x.ProductId, x.Quantity))
                .ToArray());

            var response = await bus.InvokeAsync<CreatedResponse>(cmd, ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverinePost("/api/orders/{id:guid}/confirm")]
    public static Task<IResult> ConfirmOrder(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new ConfirmOrderCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/cancel")]
    public static Task<IResult> CancelOrder(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new CancelOrderCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/fulfill")]
    public static Task<IResult> FulfillOrder(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new StartFulfillmentCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/deliver")]
    public static Task<IResult> DeliverOrder(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new DeliverOrderCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/refund/request")]
    public static Task<IResult> RequestRefund(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new RequestRefundCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/refund/complete")]
    public static Task<IResult> CompleteRefund(Guid id, IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new CompleteRefundCommand(id), ct);

    [WolverinePost("/api/orders/{id:guid}/ship")]
    public static async Task<IResult> ShipOrder(Guid id, ShipOrderRequest request, IMessageBus bus, CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<OperationResponse>(new ShipOrderCommand(id, request.TrackingNumber), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverineGet("/api/orders/{id:guid}")]
    public static async Task<IResult> GetOrder(Guid id, IMessageBus bus, CancellationToken ct)
    {
        var order = await bus.InvokeAsync<OrderDetailsResponse?>(new GetOrderByIdQuery(id), ct);

        if (order is null)
        {
            return Results.NotFound(new { error = "Order not found." });
        }

        return Results.Ok(order);
    }

    [WolverineGet("/api/orders")]
    public static async Task<IResult> GetOrders(IMessageBus bus, int? page = null, int? pageSize = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await bus.InvokeAsync<PagedResponse<OrderSummaryResponse>>(
                new GetOrdersQuery(page, pageSize), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> InvokeOperation<TCommand>(IMessageBus bus, TCommand command, CancellationToken ct)
        where TCommand : notnull
    {
        try
        {
            var response = await bus.InvokeAsync<OperationResponse>(command, ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
