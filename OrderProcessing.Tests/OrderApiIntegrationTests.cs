using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using Xunit;

namespace OrderProcessing.Tests;

public class OrderApiIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public OrderApiIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConcurrentConfirm_ShouldReserveOnlyAvailableStock()
    {
        await _factory.ResetDatabaseAsync();

        var productId = await CreateProductAsync("Concurrency Product", $"CONC-{Guid.NewGuid():N}"[..16], 10m, 15);

        var createOrderTasks = Enumerable.Range(0, 20)
            .Select(_ => CreateOrderAsync(productId, 1))
            .ToArray();

        var orderIds = await Task.WhenAll(createOrderTasks);

        var confirmTasks = orderIds
            .Select(orderId => _client.PostAsync($"/api/orders/{orderId}/confirm", null))
            .ToArray();

        var responses = await Task.WhenAll(confirmTasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejectedCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        var productsResponse = await _client.GetAsync("/api/products?page=1&pageSize=100");
        productsResponse.EnsureSuccessStatusCode();

        var productsPage = await productsResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse products page response.");

        var product = productsPage.Items.Single(x => x.Id == productId);

        Assert.Equal(10, successCount);
        Assert.Equal(10, rejectedCount);
        Assert.Equal(0, product.AvailableStock);
    }

    [Fact]
    public async Task VerifyPayment_WithSameIdempotencyKey_ShouldBeSingleEffect()
    {
        await _factory.ResetDatabaseAsync();

        var productId = await CreateProductAsync("Idempotency Product", $"IDEM-{Guid.NewGuid():N}"[..16], 20m, 20);
        var orderId = await CreateOrderAsync(productId, 1);

        var confirm = await _client.PostAsync($"/api/orders/{orderId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var initiateResponse = await _client.PostAsJsonAsync($"/api/orders/{orderId}/pay/initiate", new InitiatePaymentRequest
        {
            CardNumber = "4111111111111111",
            ExpiryDate = "12/30",
            Cvc = "123"
        });
        initiateResponse.EnsureSuccessStatusCode();

        var initiation = await initiateResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse initiate payment response.");

        var idempotencyKey = $"idem-{Guid.NewGuid():N}";

        var verifyTasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync($"/api/orders/{orderId}/pay/verify", new VerifyPaymentRequest
            {
                VerificationToken = initiation.VerificationToken,
                IdempotencyKey = idempotencyKey
            }))
            .ToArray();

        var verifyResponses = await Task.WhenAll(verifyTasks);
        var payloads = await Task.WhenAll(verifyResponses.Select(r => r.Content.ReadAsStringAsync()));

        var firstStatus = verifyResponses[0].StatusCode;
        var firstPayload = payloads[0];

        Assert.All(verifyResponses, r => Assert.Equal(firstStatus, r.StatusCode));
        Assert.All(payloads, body => Assert.Equal(firstPayload, body));

        await WaitForVerifyCompletionAsync(orderId, idempotencyKey);

        var paymentRows = await _factory.CountPaymentsForOrderAsync(orderId);
        Assert.Equal(1, paymentRows);
    }

    private async Task<Guid> CreateProductAsync(string name, string sku, decimal price, int stock)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = name,
            Sku = sku,
            Price = price,
            InitialStock = stock
        });

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse create product response.");

        return created.Id;
    }

    private async Task<Guid> CreateOrderAsync(Guid productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            ]
        });

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse create order response.");

        return created.Id;
    }

    private async Task WaitForVerifyCompletionAsync(Guid orderId, string idempotencyKey)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var pollResponse = await _client.GetAsync($"/api/orders/{orderId}/pay/verify/{idempotencyKey}");
            if (pollResponse.StatusCode != HttpStatusCode.Accepted)
            {
                Assert.True(
                    pollResponse.StatusCode is HttpStatusCode.OK or (HttpStatusCode)402,
                    $"Unexpected final poll status code {(int)pollResponse.StatusCode}.");
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for payment verification completion.");
    }

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
