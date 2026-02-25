using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;
using Xunit;

namespace OrderProcessing.Tests;

public class OrderStateMachineTests
{
    private static readonly OrderStatus[] AllStatuses = Enum.GetValues<OrderStatus>();

    public static IEnumerable<object[]> ValidTransitions()
    {
        foreach (var from in AllStatuses)
        {
            var allowed = Order.AllowedFrom(from);
            foreach (var to in allowed)
            {
                yield return [from, to];
            }
        }
    }

    public static IEnumerable<object[]> InvalidTransitions()
    {
        foreach (var from in AllStatuses)
        {
            var allowed = Order.AllowedFrom(from).ToHashSet();

            foreach (var to in AllStatuses)
            {
                if (to == from || allowed.Contains(to))
                {
                    continue;
                }

                yield return [from, to];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void ValidTransition_ShouldSucceed(OrderStatus from, OrderStatus to)
    {
        var order = BuildOrderInStatus(from);

        order.TransitionTo(to);

        Assert.Equal(to, order.Status);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void InvalidTransition_ShouldThrow(OrderStatus from, OrderStatus to)
    {
        var order = BuildOrderInStatus(from);

        var ex = Assert.Throws<DomainValidationException>(() => order.TransitionTo(to));
        Assert.Contains($"Current status is {from}", ex.Message);
        Assert.Contains($"Allowed transitions from {from}", ex.Message);
    }

    private static Order BuildOrderInStatus(OrderStatus status)
    {
        var order = Order.Create([(Guid.NewGuid(), 1, 10m)]);

        if (status == OrderStatus.Draft)
        {
            return order;
        }

        var path = BuildPathFromDraft(status);
        foreach (var step in path)
        {
            order.TransitionTo(step);
        }

        return order;
    }

    private static IReadOnlyList<OrderStatus> BuildPathFromDraft(OrderStatus target)
    {
        var result = new List<OrderStatus>();
        var visited = new HashSet<OrderStatus> { OrderStatus.Draft };

        if (Dfs(OrderStatus.Draft, target, visited, result))
        {
            return result;
        }

        throw new InvalidOperationException($"Could not build path from Draft to {target}.");
    }

    private static bool Dfs(OrderStatus current, OrderStatus target, HashSet<OrderStatus> visited, List<OrderStatus> path)
    {
        if (current == target)
        {
            return true;
        }

        foreach (var next in Order.AllowedFrom(current))
        {
            if (!visited.Add(next))
            {
                continue;
            }

            path.Add(next);
            if (Dfs(next, target, visited, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(next);
        }

        return false;
    }
}
