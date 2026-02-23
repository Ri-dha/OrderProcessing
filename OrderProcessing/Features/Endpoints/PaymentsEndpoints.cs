using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.errors;
using Wolverine;
using Wolverine.Http;

namespace OrderProcessing.Features;

public static class PaymentsEndpoints
{
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
    public static async Task<IResult> PollVerifyPaymentStatus(Guid id, string idempotencyKey, IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<VerifyPaymentResult>(
            new PollPaymentVerificationStatusQuery(id, idempotencyKey), ct);

        if (result.Response is null)
        {
            return Results.Json(new { message = result.Message }, statusCode: result.StatusCode);
        }

        return Results.Json(result.Response, statusCode: result.StatusCode);
    }
}
