using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderProcessing.Application.Features;
using OrderProcessing.Features;
using Xunit;

namespace OrderProcessing.Tests;

public class OrderApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrderApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(Skip = "Requires PostgreSQL test container; run in dockerized integration environment.")]
    public async Task CreateConfirmCancelFlow_ShouldReturnSuccessCodes()
    {
        var product = await _client.PostAsJsonAsync("/api/products",
            new CreateProductRequest
            {
                Name = "Keyboard",
                Sku = "KB-1",
                Price = 50m,
                InitialStock = 3
            });
        Assert.Equal(HttpStatusCode.OK, product.StatusCode);
        var createdProduct = await product.Content.ReadFromJsonAsync<CreatedResponse>();

        var orderCreate = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest
            {
                Items =
                [
                    new CreateOrderItemRequest
                    {
                        ProductId = createdProduct!.Id,
                        Quantity = 1
                    }
                ]
            });
        Assert.Equal(HttpStatusCode.OK, orderCreate.StatusCode);
        var createdOrder = await orderCreate.Content.ReadFromJsonAsync<CreatedResponse>();

        var confirm = await _client.PostAsync($"/api/orders/{createdOrder!.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var cancel = await _client.PostAsync($"/api/orders/{createdOrder.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }
}
