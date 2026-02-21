using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Features;

public class OrderCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreatedResponse> Handle(CreateProductCommand command, AppDbContext db, CancellationToken ct)
    {
        var product = new Product(command.Name, command.Sku, command.Price, command.InitialStock);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return new CreatedResponse(product.Id, "Product created.");
    }

    public async Task<CreatedResponse> Handle(CreateOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        if (command.Items.Count == 0)
        {
            throw new DomainValidationException("Order must include at least one line item.");
        }

        var requestedProductIds = command.Items.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products
            .Where(p => requestedProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        if (products.Count != requestedProductIds.Length)
        {
            throw new DomainValidationException("One or more products do not exist.");
        }

        var lines = command.Items.Select(item =>
        {
            if (item.Quantity <= 0)
            {
                throw new DomainValidationException("Line item quantity must be greater than zero.");
            }

            var product = products[item.ProductId];
            return (item.ProductId, item.Quantity, product.Price);
        });

        var order = Order.Create(lines);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return new CreatedResponse(order.Id, "Order created in DRAFT status.");
    }

    public async Task<OperationResponse> Handle(ConfirmOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        const int maxRetries = 5;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var order = await db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

                if (order is null)
                {
                    throw new DomainValidationException("Order not found.");
                }

                order.TransitionTo(OrderStatus.Confirmed);

                foreach (var item in order.Items)
                {
                    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                    if (product is null)
                    {
                        throw new DomainValidationException($"Product {item.ProductId} not found.");
                    }

                    product.ReserveStock(item.Quantity);
                    db.InventoryLogs.Add(new InventoryLog(product.Id, order.Id, InventoryLogType.Reservation, item.Quantity));
                }

                await db.SaveChangesAsync(ct);
                return new OperationResponse("Order confirmed and stock reserved.", order.Id, order.Status.ToString());
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                db.ChangeTracker.Clear();
                await Task.Delay(40 * attempt, ct);
            }
        }

        throw new DomainValidationException("Could not confirm order due to concurrent updates. Please retry.");
    }

    public async Task<OperationResponse> Handle(CancelOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            foreach (var item in order.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                if (product is null)
                {
                    throw new DomainValidationException($"Product {item.ProductId} not found.");
                }

                product.ReleaseStock(item.Quantity);
                db.InventoryLogs.Add(new InventoryLog(product.Id, order.Id, InventoryLogType.Release, item.Quantity));
            }
        }

        order.TransitionTo(OrderStatus.Cancelled);
        await db.SaveChangesAsync(ct);
        return new OperationResponse("Order cancelled.", order.Id, order.Status.ToString());
    }

    public async Task<(int StatusCode, PaymentResponse Response)> Handle(ProcessPaymentCommand command, AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new DomainValidationException("Idempotency key is required.");
        }

        var existingByKey = await db.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Key == command.IdempotencyKey, ct);

        if (existingByKey is not null)
        {
            if (existingByKey.OrderId != command.OrderId)
            {
                throw new DomainValidationException(
                    "Idempotency key has already been used for a different order.");
            }

            return await WaitForCompletedResponse(existingByKey.Key, db, ct);
        }

        var record = new IdempotencyRecord(command.IdempotencyKey, command.OrderId);
        db.IdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            var existing = await db.IdempotencyRecords.FirstAsync(x => x.Key == command.IdempotencyKey, ct);

            if (existing.OrderId != command.OrderId)
            {
                throw new DomainValidationException(
                    "Idempotency key has already been used for a different order.");
            }

            return await WaitForCompletedResponse(existing.Key, db, ct);
        }

        var order = await db.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.PaymentPending);
        await db.SaveChangesAsync(ct);

        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        var failed = Random.Shared.NextDouble() < 0.2d;

        if (failed)
        {
            order.TransitionTo(OrderStatus.PaymentFailed);
            var failedPayment = Payment.Create(order.Id, order.TotalAmount(), PaymentStatus.Failed);
            db.Payments.Add(failedPayment);

            var response = new PaymentResponse(order.Id, order.Status.ToString(), order.TotalAmount(),
                "Payment failed (simulated gateway failure).");
            record.Complete(402, JsonSerializer.Serialize(response, JsonOptions));

            await db.SaveChangesAsync(ct);
            return (402, response);
        }

        order.TransitionTo(OrderStatus.Paid);
        var payment = Payment.Create(order.Id, order.TotalAmount(), PaymentStatus.Succeeded);
        db.Payments.Add(payment);

        var successResponse = new PaymentResponse(order.Id, order.Status.ToString(), payment.Amount,
            "Payment succeeded.");
        record.Complete(200, JsonSerializer.Serialize(successResponse, JsonOptions));

        await db.SaveChangesAsync(ct);
        return (200, successResponse);
    }

    public async Task<OperationResponse> Handle(StartFulfillmentCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.Fulfilling);

        foreach (var item in order.Items)
        {
            db.InventoryLogs.Add(new InventoryLog(item.ProductId, order.Id, InventoryLogType.Deduction, item.Quantity));
        }

        await db.SaveChangesAsync(ct);
        return new OperationResponse("Order is now fulfilling.", order.Id, order.Status.ToString());
    }

    public async Task<OperationResponse> Handle(ShipOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.Shipped);
        order.SetTrackingNumber(command.TrackingNumber);
        await db.SaveChangesAsync(ct);

        return new OperationResponse("Order shipped.", order.Id, order.Status.ToString());
    }

    public async Task<OperationResponse> Handle(DeliverOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.Delivered);
        await db.SaveChangesAsync(ct);

        return new OperationResponse("Order delivered.", order.Id, order.Status.ToString());
    }

    public async Task<OperationResponse> Handle(RequestRefundCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);
        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.RefundRequested);
        await db.SaveChangesAsync(ct);

        return new OperationResponse("Refund requested.", order.Id, order.Status.ToString());
    }

    public async Task<OperationResponse> Handle(CompleteRefundCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id, ct);
        if (payment is null)
        {
            throw new DomainValidationException("Cannot refund an order without a payment record.");
        }

        order.TransitionTo(OrderStatus.Refunded);
        payment.MarkRefunded();

        foreach (var item in order.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null)
            {
                throw new DomainValidationException($"Product {item.ProductId} not found.");
            }

            product.Restock(item.Quantity);
            db.InventoryLogs.Add(new InventoryLog(product.Id, order.Id, InventoryLogType.Restock, item.Quantity));
        }

        await db.SaveChangesAsync(ct);
        return new OperationResponse("Refund completed.", order.Id, order.Status.ToString());
    }

    public async Task Handle(CleanupIdempotencyRecordsCommand _, AppDbContext db, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddHours(-24);
        var staleRecords = await db.IdempotencyRecords
            .Where(x => x.CreatedAt < threshold)
            .ToListAsync(ct);

        if (staleRecords.Count == 0)
        {
            return;
        }

        db.IdempotencyRecords.RemoveRange(staleRecords);
        await db.SaveChangesAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pex)
        {
            return pex.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        return false;
    }

    private static async Task<(int StatusCode, PaymentResponse Response)> WaitForCompletedResponse(string key,
        AppDbContext db, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            var record = await db.IdempotencyRecords
                .AsNoTracking()
                .FirstAsync(x => x.Key == key, ct);

            if (record.IsCompleted)
            {
                var response = JsonSerializer.Deserialize<PaymentResponse>(record.ResponseBody!, JsonOptions)
                    ?? throw new DomainValidationException("Stored idempotency response is invalid.");

                return (record.ResponseStatusCode!.Value, response);
            }

            await Task.Delay(100, ct);
        }

        throw new DomainValidationException("Payment is still processing for this idempotency key. Try again shortly.");
    }
}
