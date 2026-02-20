using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;

namespace OrderProcessing.Features;

public static class EndpointMappings
{
    public static IEndpointRouteBuilder MapOrderProcessingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (CreateProductRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            try
            {
                var response = await bus.InvokeAsync<CreatedResponse>(
                    new CreateProductCommand(request.Name, request.Sku, request.Price, request.InitialStock), ct);
                return Results.Ok(response);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/orders", async (CreateOrderRequest request, IMessageBus bus, CancellationToken ct) =>
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
        });

        app.MapPost("/api/orders/{id:guid}/confirm", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new ConfirmOrderCommand(id), ct));
        app.MapPost("/api/orders/{id:guid}/cancel", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new CancelOrderCommand(id), ct));
        app.MapPost("/api/orders/{id:guid}/fulfill", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new StartFulfillmentCommand(id), ct));
        app.MapPost("/api/orders/{id:guid}/deliver", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new DeliverOrderCommand(id), ct));
        app.MapPost("/api/orders/{id:guid}/refund/request", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new RequestRefundCommand(id), ct));
        app.MapPost("/api/orders/{id:guid}/refund/complete", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            await InvokeOperation(bus, new CompleteRefundCommand(id), ct));

        app.MapPost("/api/orders/{id:guid}/ship", async (Guid id, ShipOrderRequest request, IMessageBus bus,
            CancellationToken ct) =>
        {
            try
            {
                var response = await bus.InvokeAsync<OperationResponse>(
                    new ShipOrderCommand(id, request.TrackingNumber), ct);
                return Results.Ok(response);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/orders/{id:guid}/pay", async (Guid id, HttpContext httpContext, IMessageBus bus,
            CancellationToken ct) =>
        {
            try
            {
                var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
                var result = await bus.InvokeAsync<(int StatusCode, PaymentResponse Response)>(
                    new ProcessPaymentCommand(id, key), ct);

                return Results.Json(result.Response, statusCode: result.StatusCode);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/orders/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (order is null)
            {
                return Results.NotFound(new { error = "Order not found." });
            }

            return Results.Ok(new OrderDetailsResponse(
                order.Id,
                order.Status.ToString(),
                order.TrackingNumber,
                order.TotalAmount(),
                order.Items.Select(x => new OrderLineResponse(x.ProductId, x.Quantity, x.UnitPrice)).ToArray()));
        });

        return app;
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
