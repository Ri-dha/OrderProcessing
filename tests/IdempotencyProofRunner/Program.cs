using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5203";
var outputPath = args.Length > 1 ? args[1] : "tests/idempotency-proof.txt";
var connectionString = args.Length > 2
    ? args[2]
    : "Host=localhost;Port=5432;Database=order_db;Username=postgres;Password=postgres";

using var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

var startedAt = DateTime.UtcNow;

var productSku = $"IDEM-{DateTime.UtcNow:yyyyMMddHHmmss}";
var idempotencyKey = $"idem-{DateTime.UtcNow:yyyyMMddHHmmss}";

var seededProduct = await CreateProductAsync(client, jsonOptions, productSku);
var orderId = await CreateOrderAsync(client, jsonOptions, seededProduct.Id);
await ConfirmOrderAsync(client, orderId);

var paymentInit = await InitiatePaymentAsync(client, jsonOptions, orderId);

var verifyTasks = Enumerable.Range(0, 10)
    .Select(_ => VerifyPaymentAsync(client, orderId, paymentInit.VerificationToken, idempotencyKey))
    .ToArray();

var verifyResponses = await Task.WhenAll(verifyTasks);

var allResponsesIdentical = verifyResponses.All(r => r.StatusCode == verifyResponses[0].StatusCode && r.Body == verifyResponses[0].Body);

var pollStatusCode = await WaitForPaymentCompletionAsync(client, orderId, idempotencyKey, timeout: TimeSpan.FromSeconds(25));
var paymentCount = await CountPaymentsForOrderAsync(connectionString, orderId);

var pass = paymentCount == 1 && allResponsesIdentical;

var sb = new StringBuilder();
sb.AppendLine("Idempotency Proof Run");
sb.AppendLine("--------------------------------");
sb.AppendLine($"StartedAtUtc: {startedAt:O}");
sb.AppendLine($"BaseUrl: {baseUrl}");
sb.AppendLine($"OrderId: {orderId}");
sb.AppendLine($"IdempotencyKey: {idempotencyKey}");
sb.AppendLine("Sent 10 concurrent verify-payment requests with SAME idempotency key");
sb.AppendLine();
sb.AppendLine("Results:");
sb.AppendLine($"- Verify response status from all calls: {(int)verifyResponses[0].StatusCode}");
sb.AppendLine($"- All 10 verify responses identical: {allResponsesIdentical}");
sb.AppendLine($"- Poll completion status: {pollStatusCode}");
sb.AppendLine($"- Payment rows for order in DB: {paymentCount}");
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

static async Task<CreatedResponse> CreateProductAsync(HttpClient client, JsonSerializerOptions jsonOptions, string sku)
{
    var response = await client.PostAsJsonAsync("api/products", new
    {
        name = "Idempotency Product",
        sku,
        price = 100m,
        initialStock = 20
    });

    response.EnsureSuccessStatusCode();

    return await response.Content.ReadFromJsonAsync<CreatedResponse>(jsonOptions)
           ?? throw new Exception("Failed to deserialize created product response.");
}

static async Task<Guid> CreateOrderAsync(HttpClient client, JsonSerializerOptions jsonOptions, Guid productId)
{
    var response = await client.PostAsJsonAsync("api/orders", new
    {
        items = new[]
        {
            new
            {
                productId,
                quantity = 1
            }
        }
    });

    response.EnsureSuccessStatusCode();

    var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(jsonOptions)
                  ?? throw new Exception("Failed to deserialize created order response.");

    return created.Id;
}

static async Task ConfirmOrderAsync(HttpClient client, Guid orderId)
{
    var response = await client.PostAsync($"api/orders/{orderId}/confirm", null);
    response.EnsureSuccessStatusCode();
}

static async Task<PaymentInitiationResponse> InitiatePaymentAsync(HttpClient client, JsonSerializerOptions jsonOptions, Guid orderId)
{
    var response = await client.PostAsJsonAsync($"api/orders/{orderId}/pay/initiate", new
    {
        cardNumber = "4111111111111111",
        expiryDate = "12/30",
        cvc = "123"
    });

    response.EnsureSuccessStatusCode();

    return await response.Content.ReadFromJsonAsync<PaymentInitiationResponse>(jsonOptions)
           ?? throw new Exception("Failed to deserialize payment initiation response.");
}

static async Task<RawResponse> VerifyPaymentAsync(HttpClient client, Guid orderId, string verificationToken, string idempotencyKey)
{
    var response = await client.PostAsJsonAsync($"api/orders/{orderId}/pay/verify", new
    {
        verificationToken,
        idempotencyKey
    });

    var body = await response.Content.ReadAsStringAsync();
    return new RawResponse(response.StatusCode, body);
}

static async Task<int> WaitForPaymentCompletionAsync(HttpClient client, Guid orderId, string idempotencyKey, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;

    while (DateTime.UtcNow < deadline)
    {
        var response = await client.GetAsync($"api/orders/{orderId}/pay/verify/{idempotencyKey}");
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            return (int)response.StatusCode;
        }

        await Task.Delay(300);
    }

    throw new TimeoutException("Timed out waiting for payment completion.");
}

static async Task<int> CountPaymentsForOrderAsync(string connectionString, Guid orderId)
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Payments\" WHERE \"OrderId\" = @orderId", conn);
    cmd.Parameters.AddWithValue("orderId", orderId);

    var count = await cmd.ExecuteScalarAsync();
    return Convert.ToInt32(count);
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
internal sealed record PaymentInitiationResponse(Guid OrderId, string Status, string VerificationToken, DateTime ExpiresAt, string Message);
internal sealed record RawResponse(HttpStatusCode StatusCode, string Body);
