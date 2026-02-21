using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Features;
using OrderProcessing.Domain.errors;
using OrderProcessing.Features;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;

namespace OrderProcessing.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var cmd = new CreateOrderCommand(request.Items
                .Select(x => new CreateOrderItemCommand(x.ProductId, x.Quantity))
                .ToArray());

            var response = await bus.InvokeAsync<CreatedResponse>(cmd, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> ConfirmOrder(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new ConfirmOrderCommand(id), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> CancelOrder(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new CancelOrderCommand(id), ct);

    [HttpPost("{id:guid}/fulfill")]
    public Task<IActionResult> FulfillOrder(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new StartFulfillmentCommand(id), ct);

    [HttpPost("{id:guid}/deliver")]
    public Task<IActionResult> DeliverOrder(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new DeliverOrderCommand(id), ct);

    [HttpPost("{id:guid}/refund/request")]
    public Task<IActionResult> RequestRefund(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new RequestRefundCommand(id), ct);

    [HttpPost("{id:guid}/refund/complete")]
    public Task<IActionResult> CompleteRefund(Guid id, [FromServices] IMessageBus bus, CancellationToken ct) =>
        InvokeOperation(bus, new CompleteRefundCommand(id), ct);

    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> ShipOrder(
        Guid id,
        [FromBody] ShipOrderRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<OperationResponse>(new ShipOrderCommand(id, request.TrackingNumber), ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/pay/initiate")]
    public async Task<IActionResult> InitiatePayment(
        Guid id,
        [FromBody] InitiatePaymentRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<PaymentInitiationResponse>(
                new InitiatePaymentCommand(id, request.CardNumber, request.ExpiryDate, request.Cvc), ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/pay/verify")]
    public async Task<IActionResult> VerifyPayment(
        Guid id,
        [FromBody] VerifyPaymentRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var result = await bus.InvokeAsync<VerifyPaymentResult>(
                new VerifyPaymentCommand(id, request.VerificationToken, request.IdempotencyKey), ct);

            return StatusCode(result.StatusCode, result.Response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (order is null)
        {
            return NotFound(new { error = "Order not found." });
        }

        return Ok(new OrderDetailsResponse(
            order.Id,
            order.Status.ToString(),
            order.TrackingNumber,
            order.TotalAmount(),
            order.Items.Select(x => new OrderLineResponse(x.ProductId, x.Quantity, x.UnitPrice)).ToArray()));
    }

    private static async Task<IActionResult> InvokeOperation<TCommand>(IMessageBus bus, TCommand command, CancellationToken ct)
        where TCommand : notnull
    {
        try
        {
            var response = await bus.InvokeAsync<OperationResponse>(command, ct);
            return new OkObjectResult(response);
        }
        catch (DomainValidationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }
}
