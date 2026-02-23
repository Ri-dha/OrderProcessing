using System.Text.Json;
using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Http;

namespace OrderProcessing.Features;

public static class PaymentsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [WolverinePost("/api/orders/{id:guid}/pay/initiate")]
    public static async Task<IResult> InitiatePayment(Guid id, InitiatePaymentRequest request, IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<PaymentInitiationResponse>(
                new InitiatePaymentCommand(id, request.CardNumber, request.ExpiryDate, request.Cvc), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverinePost("/api/orders/{id:guid}/pay/verify")]
    public static async Task<IResult> VerifyPayment(Guid id, VerifyPaymentRequest request, IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var result = await bus.InvokeAsync<VerifyPaymentResult>(
                new VerifyPaymentCommand(id, request.VerificationToken, request.IdempotencyKey), ct);

            if (result.Response is null)
            {
                return Results.Json(new { message = result.Message }, statusCode: result.StatusCode);
            }

            return Results.Json(result.Response, statusCode: result.StatusCode);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverineGet("/api/orders/{id:guid}/pay/verify/{idempotencyKey}")]
    public static async Task<IResult> PollVerifyPaymentStatus(Guid id, string idempotencyKey, AppDbContext db,
        CancellationToken ct)
    {
        var record = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == id && x.Key == idempotencyKey, ct);

        if (record is null)
        {
            return Results.NotFound(new { error = "Idempotency key not found for this order." });
        }

        if (!record.IsCompleted)
        {
            return Results.Json(new { message = "Payment processing pending." }, statusCode: 202);
        }

        var response = JsonSerializer.Deserialize<PaymentResponse>(record.ResponseBody!, JsonOptions);
        return Results.Json(response, statusCode: record.ResponseStatusCode);
    }
}
