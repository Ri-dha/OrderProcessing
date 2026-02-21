using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;
using Xunit;

namespace OrderProcessing.Tests;

public class OrderStateMachineTests
{
    [Fact]
    public void ValidTransitions_ShouldSucceed()
    {
        var order = Order.Create([(Guid.NewGuid(), 1, 10m)]);

        order.TransitionTo(OrderStatus.Confirmed);
        order.TransitionTo(OrderStatus.PaymentPending);
        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Fulfilling);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.Delivered);
        order.TransitionTo(OrderStatus.RefundRequested);
        order.TransitionTo(OrderStatus.Refunded);

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void InvalidTransition_ShouldThrowWithAllowedTransitions()
    {
        var order = Order.Create([(Guid.NewGuid(), 1, 10m)]);

        var ex = Assert.Throws<DomainValidationException>(() => order.TransitionTo(OrderStatus.Paid));
        Assert.Contains("Current status is Draft", ex.Message);
        Assert.Contains("Allowed transitions from Draft: Confirmed, Cancelled", ex.Message);
    }
}
