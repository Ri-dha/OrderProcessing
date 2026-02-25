using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Features;
using OrderProcessing.Domain.enums;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;
using System.Diagnostics;
using System.Threading;

namespace OrderProcessing.Application.Services;

public sealed class OrderExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ExpiryWindow = TimeSpan.FromMinutes(10);
    private const int BatchSize = 200;
    private long _runId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Order expiry worker started.");
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ExpireOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Order expiry worker failed.");
                }
            }
        }
        finally
        {
            logger.LogInformation("Order expiry worker stopped.");
        }
    }

    private async Task ExpireOrdersAsync(CancellationToken ct)
    {
        var runId = Interlocked.Increment(ref _runId);
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ExpiryRunId"] = runId
        });

        var startedAt = DateTime.UtcNow;
        var cutoff = startedAt - ExpiryWindow;
        logger.LogInformation(
            "Starting order expiry sweep. Cutoff: {CutoffUtc}, Window: {ExpiryWindow}.",
            cutoff,
            ExpiryWindow);

        using var serviceScope = scopeFactory.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bus = serviceScope.ServiceProvider.GetRequiredService<IMessageBus>();

        var sw = Stopwatch.StartNew();

        var expiredOrderIds = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.PaymentPending &&
                        (o.UpdatedAt ?? o.CreatedAt) <= cutoff)
            .Select(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        logger.LogInformation(
            "Expiry sweep found {ExpiredOrderCount} candidate orders (batch size {BatchSize}).",
            expiredOrderIds.Count,
            BatchSize);

        var successCount = 0;
        var failureCount = 0;

        foreach (var orderId in expiredOrderIds)
        {
            try
            {
                await bus.InvokeAsync(new CancelOrderCommand(orderId), ct);
                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                logger.LogWarning(ex, "Failed to cancel expired order {OrderId}.", orderId);
            }
        }

        sw.Stop();
        logger.LogInformation(
            "Order expiry sweep completed in {ElapsedMs} ms. Cancelled: {SuccessCount}, Failed: {FailureCount}.",
            sw.ElapsedMilliseconds,
            successCount,
            failureCount);
    }
}
