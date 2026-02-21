namespace OrderProcessing.Domain.enums;

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    PaymentPending = 2,
    Paid = 3,
    PaymentFailed = 4,
    Fulfilling = 5,
    Shipped = 6,
    Delivered = 7,
    Cancelled = 8,
    RefundRequested = 9,
    Refunded = 10
}
