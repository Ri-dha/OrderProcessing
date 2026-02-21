using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Features;

public sealed class InitiatePaymentRequest
{
    [Required, StringLength(19, MinimumLength = 13)]
    public string CardNumber { get; init; } = string.Empty;

    [Required, RegularExpression("^(0[1-9]|1[0-2])\\/[0-9]{2}$")]
    public string ExpiryDate { get; init; } = string.Empty;

    [Required, RegularExpression("^[0-9]{3,4}$")]
    public string Cvc { get; init; } = string.Empty;
}

public sealed class VerifyPaymentRequest
{
    [Required, MinLength(1)]
    public string VerificationToken { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string IdempotencyKey { get; init; } = string.Empty;
}
