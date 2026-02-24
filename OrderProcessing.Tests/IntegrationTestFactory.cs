using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Infrastructure.Persistence;
using OrderProcessing.Features;
using Xunit;

namespace OrderProcessing.Tests;

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        await EnsureDatabaseConnectivityAsync();
        await ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("""
                                             TRUNCATE TABLE
                                               "InventoryLogs",
                                               "Payments",
                                               "OrderItem",
                                               "Orders",
                                               "Products",
                                               "IdempotencyRecords",
                                               "PaymentVerificationTokens"
                                             RESTART IDENTITY CASCADE;
                                             """);
    }

    public async Task<int> CountPaymentsForOrderAsync(Guid orderId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Payments.CountAsync(x => x.OrderId == orderId);
    }

    private async Task EnsureDatabaseConnectivityAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var canConnect = await db.Database.CanConnectAsync();
        if (canConnect)
        {
            return;
        }

        throw new InvalidOperationException(
            "Integration tests require PostgreSQL at localhost:5432. Start it with `docker compose -f docker-compose.yml up -d postgres`.");
    }
}
