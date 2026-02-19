namespace OrderProcessing.Domain.enums;

public enum PaymentStatus
{
    PaymentPending,
    Paid,
    PaymentFailed,
    RefundRequested,
    RefundCompleted
}