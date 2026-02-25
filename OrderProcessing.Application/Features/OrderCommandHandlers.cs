using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.entities;
using OrderProcessing.Domain.enums;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;

namespace OrderProcessing.Application.Features;

public class OrderCommandHandler
{
    private readonly ILogger<OrderCommandHandler> _logger;

    public OrderCommandHandler(ILogger<OrderCommandHandler> logger)
    {
        _logger = logger;
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResponse<ProductResponse>> Handle(GetProductsQuery query, AppDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Handling GetProductsQuery with page {Page} and pageSize {PageSize}", query.Page, query.PageSize);

        var page = query.Page ?? 1;
        var requestedPageSize = query.PageSize ?? 20;

        if (page <= 0 || requestedPageSize <= 0)
        {
            _logger.LogWarning("Invalid page or pageSize provided. page: {Page}, pageSize: {PageSize}", page, requestedPageSize);
            throw new DomainValidationException("page and pageSize must be greater than 0.");
        }

        var pageSize = Math.Min(requestedPageSize, 100);
        var totalCount = await db.Products.CountAsync(ct);

        var items = await db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductResponse(x.Id, x.Name, x.Sku, x.Price, x.AvailableStock, x.IsDeleted))
            .ToListAsync(ct);

        return new PagedResponse<ProductResponse>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResponse<OrderSummaryResponse>> Handle(GetOrdersQuery query, AppDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Handling GetOrdersQuery with page {Page} and pageSize {PageSize}", query.Page, query.PageSize);
        var page = query.Page ?? 1;
        var requestedPageSize = query.PageSize ?? 20;

        if (page <= 0 || requestedPageSize <= 0)
        {
            _logger.LogWarning("Invalid page or pageSize provided. page: {Page}, pageSize: {PageSize}", page, requestedPageSize);
            throw new DomainValidationException("page and pageSize must be greater than 0.");
        }

        var pageSize = Math.Min(requestedPageSize, 100);
        var totalCount = await db.Orders.CountAsync(ct);

        var items = await db.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrderSummaryResponse(
                x.Id,
                x.Status.ToString(),
                x.TrackingNumber,
                x.Items.Sum(i => i.Quantity * i.UnitPrice),
                x.Items.Count,
                x.CreatedAt))
            .ToListAsync(ct);

        return new PagedResponse<OrderSummaryResponse>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDetailsResponse?> Handle(GetOrderByIdQuery query, AppDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Handling GetOrderByIdQuery with orderId {OrderId}", query.OrderId);
        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == query.OrderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Order not found with orderId {OrderId}", query.OrderId);
            return null;
        }

        return new OrderDetailsResponse(
            order.Id,
            order.Status.ToString(),
            order.TrackingNumber,
            order.TotalAmount(),
            order.Items.Select(x => new OrderLineResponse(x.ProductId, x.Quantity, x.UnitPrice)).ToArray());
    }

    public async Task<IReadOnlyList<InventoryLogResponse>> Handle(GetOrderInventoryLogsQuery query, AppDbContext db,
        CancellationToken ct)
    {
        _logger.LogInformation("Handling GetOrderInventoryLogsQuery with orderId {OrderId}", query.OrderId);
        return await db.InventoryLogs
            .AsNoTracking()
            .Where(x => x.OrderId == query.OrderId)
            .OrderBy(x => x.Timestamp)
            .Select(x => new InventoryLogResponse(
                x.Id,
                x.ProductId,
                x.OrderId,
                x.Type.ToString(),
                x.Quantity,
                x.Timestamp))
            .ToListAsync(ct);
    }

    public async Task<VerifyPaymentResult> Handle(PollPaymentVerificationStatusQuery query, AppDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Handling PollPaymentVerificationStatusQuery with orderId {OrderId} and idempotencyKey {IdempotencyKey}", query.OrderId, query.IdempotencyKey);
        var record = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == query.OrderId && x.Key == query.IdempotencyKey, ct);

        if (record is null)
        {
            _logger.LogWarning("Idempotency record not found for orderId {OrderId} and idempotencyKey {IdempotencyKey}", query.OrderId, query.IdempotencyKey);
            return new VerifyPaymentResult(404, null, "Idempotency key not found for this order.");
        }

        if (!record.IsCompleted)
        {
            return PendingResult();
        }

        if (!record.ResponseStatusCode.HasValue || string.IsNullOrWhiteSpace(record.ResponseBody))
        {
            _logger.LogWarning("Stored payment response is incomplete for orderId {OrderId} and idempotencyKey {IdempotencyKey}", query.OrderId, query.IdempotencyKey);
            return new VerifyPaymentResult(500, null, "Stored payment response is incomplete.");
        }

        var response = JsonSerializer.Deserialize<PaymentResponse>(record.ResponseBody!, JsonOptions);
        if (response is null)
        {
            _logger.LogWarning("Stored payment response is invalid for orderId {OrderId} and idempotencyKey {IdempotencyKey}", query.OrderId, query.IdempotencyKey);
            return new VerifyPaymentResult(500, null, "Stored payment response is invalid.");
        }

        return new VerifyPaymentResult(record.ResponseStatusCode.Value, response, null);
    }

    public async Task<CreatedResponse> Handle(CreateProductCommand command, AppDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Handling CreateProductCommand with name {Name}", command.Name);
        var product = new Product(command.Name, command.Sku, command.Price, command.InitialStock);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Product created with id {Id} and name {Name}", product.Id, product.Name);
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
        await db.SaveChangesAsync(ct);
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

        if (command.Name is not null)
        {
            product.UpdateName(command.Name);
        }

        if (command.Sku is not null)
        {
            product.UpdateSku(command.Sku);
        }

        if (command.Price.HasValue)
        {
            product.UpdatePrice(command.Price.Value);
        }

        if (command.Stock.HasValue)
        {
            product.UpdateStock(command.Stock.Value);
        }

        if (command.IsDeleted.HasValue)
        {
            product.SetDeleted(command.IsDeleted.Value);
        }

        await db.SaveChangesAsync(ct);
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
        await db.SaveChangesAsync(ct);
        return new CreatedResponse(order.Id, "Order created in DRAFT status.");
    }

    public async Task<(OperationResponse, OutgoingMessages)> Handle(ConfirmOrderCommand command, AppDbContext db, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            throw new DomainValidationException("Order not found.");
        }

        order.TransitionTo(OrderStatus.Confirmed);
    
        // YOUR EXCELLENT OPTIMIZATION: Bulk fetch to avoid N+1 queries
        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var messages = new OutgoingMessages();

        foreach (var item in order.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new DomainValidationException($"Product {item.ProductId} not found.");
            }

            product.ReserveStock(item.Quantity);
        
            // Yield the cascading message for the background outbox
            messages.Add(new StockReservedEvent(product.Id, order.Id, item.Quantity));
        }

        // Notice: 
        // 1. No `await db.SaveChangesAsync(ct);` (Wolverine does it automatically)
        // 2. No `try/catch` or `for` loop (Wolverine retries it automatically)
    
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

        if (order.Status is OrderStatus.Confirmed or OrderStatus.PaymentFailed)
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
            await db.SaveChangesAsync(ct);
            return (new OperationResponse("Order cancelled.", order.Id, order.Status.ToString()), messages);
        }

        order.TransitionTo(OrderStatus.Cancelled);
        await db.SaveChangesAsync(ct);
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

        if (order.Status is OrderStatus.Confirmed or OrderStatus.PaymentFailed)
        {
            order.TransitionTo(OrderStatus.PaymentPending);
        }
        else if (order.Status != OrderStatus.PaymentPending)
        {
            throw new DomainValidationException(
                $"Cannot initiate payment. Current status is {order.Status}. Allowed status: Confirmed, PaymentFailed, or PaymentPending.");
        }

        var token = PaymentVerificationToken.Create(order.Id, TimeSpan.FromMinutes(5));
        db.PaymentVerificationTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return new PaymentInitiationResponse(
            order.Id,
            order.Status.ToString(),
            token.Token,
            token.ExpiresAt,
            "Payment initiated. Verify with the short-lived token.");
    }

    public async Task<(VerifyPaymentResult, OutgoingMessages)> Handle(VerifyPaymentCommand command, AppDbContext db,
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

            return existingByKey.IsCompleted
                ? (DeserializeStoredResult(existingByKey), new OutgoingMessages())
                : (PendingResult(), new OutgoingMessages());
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

            return existing.IsCompleted
                ? (DeserializeStoredResult(existing), new OutgoingMessages())
                : (PendingResult(), new OutgoingMessages());
        }

        var messages = new OutgoingMessages
        {
            new ProcessPaymentVerificationCommand(command.OrderId, command.VerificationToken, command.IdempotencyKey)
        };

        return (PendingResult(), messages);
    }

    public async Task Handle(ProcessPaymentVerificationCommand command, AppDbContext db, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Key == command.IdempotencyKey && x.OrderId == command.OrderId, ct);

        if (record is null || record.IsCompleted)
        {
            return;
        }

        var order = await db.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == command.OrderId, ct);

        if (order is null)
        {
            var notFound = new PaymentResponse(command.OrderId, OrderStatus.PaymentFailed.ToString(), 0m, "Order not found.");
            record.Complete(404, JsonSerializer.Serialize(notFound, JsonOptions));
            await db.SaveChangesAsync(ct);
            return;
        }

        if (order.Status != OrderStatus.PaymentPending)
        {
            var invalidStatus = new PaymentResponse(order.Id, order.Status.ToString(), order.TotalAmount(),
                "Order is not in PAYMENT_PENDING state.");
            record.Complete(409, JsonSerializer.Serialize(invalidStatus, JsonOptions));
            await db.SaveChangesAsync(ct);
            return;
        }

        var token = await db.PaymentVerificationTokens
            .FirstOrDefaultAsync(x => x.OrderId == command.OrderId && x.Token == command.VerificationToken, ct);

        if (token is null || token.IsUsed() || token.IsExpired())
        {
            var invalidToken = new PaymentResponse(order.Id, order.Status.ToString(), order.TotalAmount(),
                "Payment verification token is invalid or expired.");
            record.Complete(400, JsonSerializer.Serialize(invalidToken, JsonOptions));
            await db.SaveChangesAsync(ct);
            return;
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
            return;
        }

        order.TransitionTo(OrderStatus.Paid);
        var payment = Payment.Create(order.Id, order.TotalAmount(), PaymentStatus.Succeeded);
        db.Payments.Add(payment);

        var successResponse = new PaymentResponse(order.Id, order.Status.ToString(), payment.Amount,
            "Payment succeeded.");
        record.Complete(200, JsonSerializer.Serialize(successResponse, JsonOptions));

        await db.SaveChangesAsync(ct);
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
            messages.Add(new FulfillmentCommittedEvent(item.ProductId, order.Id, item.Quantity));
        }

        await db.SaveChangesAsync(ct);
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

        await db.SaveChangesAsync(ct);
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
        await db.SaveChangesAsync(ct);
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

    private static VerifyPaymentResult PendingResult() =>
        new(202, null, "Payment verification accepted. Poll status endpoint.");

    private static VerifyPaymentResult DeserializeStoredResult(IdempotencyRecord record)
    {
        var response = JsonSerializer.Deserialize<PaymentResponse>(record.ResponseBody!, JsonOptions)
                       ?? throw new DomainValidationException("Stored idempotency response is invalid.");

        return new VerifyPaymentResult(record.ResponseStatusCode!.Value, response);
    }
}
