using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5203";
var outputPath = args.Length > 1 ? args[1] : "tests/concurrency-proof.txt";

using var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

var startedAt = DateTime.UtcNow;

var productSku = $"CONC-{DateTime.UtcNow:yyyyMMddHHmmss}";
var seedRequest = new
{
    name = "Concurrency Product",
    sku = productSku,
    price = 10m,
    initialStock = 10
};

var seedResponse = await client.PostAsJsonAsync("api/products", seedRequest);
if (!seedResponse.IsSuccessStatusCode)
{
    throw new Exception($"Failed to seed product. Status={(int)seedResponse.StatusCode}");
}

var createdProduct = await seedResponse.Content.ReadFromJsonAsync<CreatedResponse>(jsonOptions)
    ?? throw new Exception("Could not deserialize seeded product response.");

var createOrderTasks = Enumerable.Range(0, 20)
    .Select(_ => client.PostAsJsonAsync("api/orders", new
    {
        items = new[]
        {
            new
            {
                productId = createdProduct.Id,
                quantity = 1
            }
        }
    }))
    .ToArray();

await Task.WhenAll(createOrderTasks);

var orderResults = new List<(Guid OrderId, HttpStatusCode Status)>();
foreach (var task in createOrderTasks)
{
    var response = task.Result;
    if (!response.IsSuccessStatusCode)
    {
        continue;
    }

    var createdOrder = await response.Content.ReadFromJsonAsync<CreatedResponse>(jsonOptions);
    if (createdOrder is not null)
    {
        orderResults.Add((createdOrder.Id, response.StatusCode));
    }
}

var confirmTasks = orderResults
    .Select(x => ConfirmOrderAsync(client, x.OrderId))
    .ToArray();

var confirmResults = await Task.WhenAll(confirmTasks);

var confirmedCount = confirmResults.Count(x => x.IsConfirmed);
var rejectedCount = confirmResults.Count(x => !x.IsConfirmed);

var productsResponse = await client.GetAsync("api/products?page=1&pageSize=100");
productsResponse.EnsureSuccessStatusCode();

var productsPage = await productsResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>(jsonOptions)
    ?? throw new Exception("Could not deserialize paged products response.");

var seededProduct = productsPage.Items.FirstOrDefault(x => x.Id == createdProduct.Id)
    ?? throw new Exception("Seeded product not found in products list.");

var pass = confirmedCount == 10 && rejectedCount == 10 && seededProduct.AvailableStock == 0;

var sb = new StringBuilder();
sb.AppendLine("Concurrency Proof Run");
sb.AppendLine("--------------------------------");
sb.AppendLine($"StartedAtUtc: {startedAt:O}");
sb.AppendLine($"BaseUrl: {baseUrl}");
sb.AppendLine($"Seeded Product ID={createdProduct.Id}, SKU={productSku}, InitialStock=10");
sb.AppendLine("Created 20 draft orders, each requesting quantity=1");
sb.AppendLine("Ran 20 concurrent confirmations");
sb.AppendLine();
sb.AppendLine("Results:");
sb.AppendLine($"- Confirmed orders: {confirmedCount}");
sb.AppendLine($"- Rejected orders: {rejectedCount}");
sb.AppendLine($"- Final stock: {seededProduct.AvailableStock}");
sb.AppendLine($"- Total confirm attempts: {confirmResults.Length}");
sb.AppendLine();
sb.AppendLine($"Assertion: {(pass ? "PASS" : "FAIL")}");

var output = sb.ToString();
Console.WriteLine(output);

var fullOutputPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
await File.WriteAllTextAsync(fullOutputPath, output);

var archivedPath = BuildTimestampedPath(fullOutputPath, startedAt);
await File.WriteAllTextAsync(archivedPath, output);
Console.WriteLine($"Saved latest proof: {fullOutputPath}");
Console.WriteLine($"Saved archived proof: {archivedPath}");

if (!pass)
{
    Environment.ExitCode = 1;
}

return;

static async Task<ConfirmResult> ConfirmOrderAsync(HttpClient client, Guid orderId)
{
    var response = await client.PostAsync($"api/orders/{orderId}/confirm", content: null);
    return new ConfirmResult(orderId, response.IsSuccessStatusCode, response.StatusCode);
}

static string BuildTimestampedPath(string fullOutputPath, DateTime timestampUtc)
{
    var directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
    var fileName = Path.GetFileNameWithoutExtension(fullOutputPath);
    var extension = Path.GetExtension(fullOutputPath);
    var timestamp = timestampUtc.ToString("yyyyMMdd-HHmmss");
    return Path.Combine(directory, $"{fileName}-{timestamp}{extension}");
}

internal sealed record CreatedResponse(Guid Id, string Message);

internal sealed record ProductResponse(Guid Id, string Name, string Sku, decimal Price, int AvailableStock, bool IsDeleted);

internal sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

internal sealed record ConfirmResult(Guid OrderId, bool IsConfirmed, HttpStatusCode StatusCode);
