using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;

namespace OrderProcessing.Application.Features;

public class OrderCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreatedResponse> Handle(CreateProductCommand command, AppDbContext db, CancellationToken ct)
    {
        var product = new Product(command.Name, command.Sku, command.Price, command.InitialStock);
        db.Products.Add(product);
        return new CreatedResponse(product.Id, "Product created.");
    }

    public async Task<BulkCreatedResponse> Handle(CreateProductsBulkCommand command, AppDbContext db, CancellationToken ct)
    {
        if (command.Products.Count == 0)
        {
            throw new DomainValidationException("At least one product is required.");
        }

        var products = command.Products
            .Select(x => new Product(x.Name, x.Sku, x.Price, x.InitialStock))
            .ToList();

        db.Products.AddRange(products);
        return new BulkCreatedResponse(products.Count, products.Select(x => x.Id).ToArray(), "Products created.");
    }

    public async Task<ProductResponse> Handle(UpdateProductCommand command, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == command.ProductId, ct);

        if (product is null)
        {
            throw new DomainValidationException("Product not found.");
        }

        product.UpdateDetails(command.Name, command.Sku, command.Price, command.Stock, command.IsDeleted);
        return new ProductResponse(product.Id, product.Name, product.Sku, product.Price, product.AvailableStock, product.IsDeleted);
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
        return new CreatedResponse(order.Id, "Order created in DRAFT status.");
    }

    public async Task<(OperationResponse, OutgoingMessages)> Handle(ConfirmOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null) throw new DomainValidationException("Order not found.");

        order.TransitionTo(OrderStatus.Confirmed);

        var messages = new OutgoingMessages();

        foreach (var item in order.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null) throw new DomainValidationException($"Product {item.ProductId} not found.");

            product.ReserveStock(item.Quantity);
        
           
            messages.Add(new StockReservedEvent(product.Id, order.Id, item.Quantity));
        }

        return (
            new OperationResponse("Order confirmed and stock reserved.", order.Id, order.Status.ToString()), 
            messages
        );
    }

    public async Task<(OperationResponse, OutgoingMessages)> Handle(CancelOrderCommand command, AppDbContext db, CancellationToken ct)
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
            var messages = new OutgoingMessages();

            foreach (var item in order.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                if (product is null)
                {
                    throw new DomainValidationException($"Product {item.ProductId} not found.");
                }

                product.ReleaseStock(item.Quantity);
                messages.Add(new StockReleasedEvent(product.Id, order.Id, item.Quantity));
            }

            order.TransitionTo(OrderStatus.Cancelled);
            return (new OperationResponse("Order cancelled.", order.Id, order.Status.ToString()), messages);
        }

        order.TransitionTo(OrderStatus.Cancelled);
        return (new OperationResponse("Order cancelled.", order.Id, order.Status.ToString()), new OutgoingMessages());
    }

    public async Task<PaymentInitiationResponse> Handle(InitiatePaymentCommand command, AppDbContext db, CancellationToken ct)
    {
        ValidateCardInput(command.CardNumber, command.ExpiryDate, command.Cvc);

        var order = await db.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            order.TransitionTo(OrderStatus.PaymentPending);
        }
        else if (order.Status != OrderStatus.PaymentPending)
        {
            throw new DomainValidationException(
                $"Cannot initiate payment. Current status is {order.Status}. Allowed status: Confirmed or PaymentPending.");
        }

        var token = PaymentVerificationToken.Create(order.Id, TimeSpan.FromMinutes(5));
        db.PaymentVerificationTokens.Add(token);
        return new PaymentInitiationResponse(
            order.Id,
            order.Status.ToString(),
            token.Token,
            token.ExpiresAt,
            "Payment initiated. Verify with the short-lived token.");
    }

    public async Task<VerifyPaymentResult> Handle(VerifyPaymentCommand command, AppDbContext db,
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
                throw new DomainValidationException("Idempotency key has already been used for a different order.");
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
                throw new DomainValidationException("Idempotency key has already been used for a different order.");
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

        if (order.Status != OrderStatus.PaymentPending)
        {
            throw new DomainValidationException(
                $"Cannot verify payment. Current status is {order.Status}. Allowed status: PaymentPending.");
        }

        var token = await db.PaymentVerificationTokens
            .FirstOrDefaultAsync(x => x.OrderId == command.OrderId && x.Token == command.VerificationToken, ct);

        if (token is null)
        {
            throw new DomainValidationException("Invalid payment verification token.");
        }

        if (token.IsUsed())
        {
            throw new DomainValidationException("Payment verification token has already been used.");
        }

        if (token.IsExpired())
        {
            throw new DomainValidationException("Payment verification token has expired.");
        }

        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var failed = Random.Shared.NextDouble() < 0.2d;
        token.MarkUsed();

        if (failed)
        {
            order.TransitionTo(OrderStatus.PaymentFailed);
            var failedPayment = Payment.Create(order.Id, order.TotalAmount(), PaymentStatus.Failed);
            db.Payments.Add(failedPayment);

            var response = new PaymentResponse(order.Id, order.Status.ToString(), order.TotalAmount(),
                "Payment failed (simulated gateway failure).");
            record.Complete(402, JsonSerializer.Serialize(response, JsonOptions));

            await db.SaveChangesAsync(ct);
            return new VerifyPaymentResult(402, response);
        }

        order.TransitionTo(OrderStatus.Paid);
        var payment = Payment.Create(order.Id, order.TotalAmount(), PaymentStatus.Succeeded);
        db.Payments.Add(payment);

        var successResponse = new PaymentResponse(order.Id, order.Status.ToString(), payment.Amount,
            "Payment succeeded.");
        record.Complete(200, JsonSerializer.Serialize(successResponse, JsonOptions));

        await db.SaveChangesAsync(ct);
        return new VerifyPaymentResult(200, successResponse);
    }

    public async Task<(OperationResponse, OutgoingMessages)> Handle(StartFulfillmentCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.Fulfilling);
        var messages = new OutgoingMessages();

        foreach (var item in order.Items)
        {
            messages.Add(new StockDeductedEvent(item.ProductId, order.Id, item.Quantity));
        }

        return (new OperationResponse("Order is now fulfilling.", order.Id, order.Status.ToString()), messages);
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
        return new OperationResponse("Refund requested.", order.Id, order.Status.ToString());
    }

    public async Task<(OperationResponse, OutgoingMessages)> Handle(CompleteRefundCommand command, AppDbContext db, CancellationToken ct)
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
        var messages = new OutgoingMessages();

        foreach (var item in order.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null)
            {
                throw new DomainValidationException($"Product {item.ProductId} not found.");
            }

            product.Restock(item.Quantity);
            messages.Add(new StockRestockedEvent(product.Id, order.Id, item.Quantity));
        }

        return (new OperationResponse("Refund completed.", order.Id, order.Status.ToString()), messages);
    }

    public async Task Handle(CleanupIdempotencyRecordsCommand _, AppDbContext db, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddHours(-24);
        var staleRecords = await db.IdempotencyRecords
            .Where(x => x.CreatedAt < threshold)
            .ToListAsync(ct);
        var staleTokens = await db.PaymentVerificationTokens
            .Where(x => x.ExpiresAt < DateTime.UtcNow || x.UsedAt != null)
            .ToListAsync(ct);

        if (staleRecords.Count == 0 && staleTokens.Count == 0)
        {
            return;
        }

        db.IdempotencyRecords.RemoveRange(staleRecords);
        db.PaymentVerificationTokens.RemoveRange(staleTokens);
    }

    private static void ValidateCardInput(string cardNumber, string expiryDate, string cvc)
    {
        var normalizedCard = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (normalizedCard.Length is < 13 or > 19)
        {
            throw new DomainValidationException("Card number must be between 13 and 19 digits.");
        }

        if (!IsLuhnValid(normalizedCard))
        {
            throw new DomainValidationException("Card number is invalid.");
        }

        if (!DateTime.TryParseExact(expiryDate, "MM/yy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exp))
        {
            throw new DomainValidationException("Expiry date must be in MM/yy format.");
        }

        var expMonthEnd = new DateTime(exp.Year, exp.Month, DateTime.DaysInMonth(exp.Year, exp.Month), 23, 59, 59,
            DateTimeKind.Utc);
        if (expMonthEnd < DateTime.UtcNow)
        {
            throw new DomainValidationException("Card has expired.");
        }

        if (string.IsNullOrWhiteSpace(cvc) || cvc.Length is < 3 or > 4 || !cvc.All(char.IsDigit))
        {
            throw new DomainValidationException("CVC must be 3 or 4 digits.");
        }
    }

    private static bool IsLuhnValid(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pex)
        {
            return pex.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        return false;
    }

    private static async Task<VerifyPaymentResult> WaitForCompletedResponse(string key,
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

                return new VerifyPaymentResult(record.ResponseStatusCode!.Value, response);
            }

            await Task.Delay(100, ct);
        }

        throw new DomainValidationException("Payment is still processing for this idempotency key. Try again shortly.");
    }
}
