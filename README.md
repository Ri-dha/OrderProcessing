# Order Processing Service

## Tech Stack
- .NET 10
- ASP.NET Core Web API (Wolverine.HTTP)
- Wolverine
- EF Core + PostgreSQL

## Prerequisites
- .NET SDK 10 (`dotnet --info`)
- Docker setup (for PostgreSQL or full compose run)

## Build
From `/OrderProcessing`:

```bash
dotnet restore OrderProcessing.sln
dotnet build OrderProcessing.sln
dotnet test OrderProcessing.sln
```

Automated integration tests use PostgreSQL. Start DB first:
```bash
docker compose -f docker-compose.yml up -d postgres
dotnet test OrderProcessing.sln
```

## Run Locally (Recommended)
1. Start only PostgreSQL:
```bash
docker compose -f docker-compose.yml up -d postgres
```

2. Run the API:
```bash
dotnet run --project OrderProcessing/OrderProcessing.csproj
```

Notes:
- Connection string is already configured in `OrderProcessing/appsettings.Development.json`.
- DB migrations are applied automatically on startup.
- Default local URL from launch profile: `http://localhost:5203`.

## Swagger (Local)
Swagger is enabled in `Development` environment.
When you run with `dotnet run`, it starts in `Development`, so open:

- `http://localhost:5203/swagger`

How to use it:
1. Open `/swagger`.
2. Expand an endpoint (example: `POST /api/products`).
3. Click `Try it out`.
4. Fill request JSON.
5. Click `Execute`.
6. Use returned IDs in next endpoints (order/payment flow).

## Structured Logging and Correlation ID
The API uses Serilog with request correlation.

- Request header: `X-Correlation-ID`
- If provided by client, it is reused.
- If not provided, the API generates one and returns it in the response header.
- The same correlation ID appears in HTTP logs, application logs, and EF Core DB command logs.

Example request with explicit correlation ID:
```bash
curl -X POST "http://localhost:5203/api/orders/{id}/confirm" \\
  -H "X-Correlation-ID: demo-trace-001"
```

## Run with Docker (API + DB)
```bash
docker compose -f docker-compose.yml up --build
```

- API base URL: `http://localhost:8080`

Important:
- Swagger is only enabled in `Development`.
- If Docker app service runs as `Production`, `/swagger` will return 404.
- To use Swagger in Docker, set `ASPNETCORE_ENVIRONMENT=Development` in `docker-compose.yml` under `api.environment`.

## Key Endpoints
- `POST /api/products`
- `POST /api/products/bulk`
- `GET /api/products`
- `PUT /api/products/{id}`
- `POST /api/orders`
- `GET /api/orders`
- `GET /api/orders/{id}`
- `GET /api/orders/{id}/inventory-logs`
- `POST /api/orders/{id}/confirm`
- `POST /api/orders/{id}/cancel`
- `POST /api/orders/{id}/pay/initiate`
- `POST /api/orders/{id}/pay/verify`
- `GET /api/orders/{id}/pay/verify/{idempotencyKey}`
- `POST /api/orders/{id}/fulfill`
- `POST /api/orders/{id}/ship`
- `POST /api/orders/{id}/deliver`
- `POST /api/orders/{id}/refund/request`
- `POST /api/orders/{id}/refund/complete`

## Required Proof Artifacts
- `tests/concurrency-proof.txt`
- `tests/idempotency-proof.txt`

## Concurrency Proof Runner
Run while API is running locally:

```bash
dotnet run --project tests/ConcurrencyProofRunner/ConcurrencyProofRunner.csproj
```

Optional args:

```bash
dotnet run --project tests/ConcurrencyProofRunner/ConcurrencyProofRunner.csproj -- http://localhost:5203 tests/concurrency-proof.txt
```

Each run writes:
- `tests/concurrency-proof.txt` (latest)
- `tests/concurrency-proof-YYYYMMDD-HHMMSS.txt` (archived)

## Idempotency Proof Runner
Run while API + PostgreSQL are running:

```bash
dotnet run --project tests/IdempotencyProofRunner/IdempotencyProofRunner.csproj
```

Optional args:

```bash
dotnet run --project tests/IdempotencyProofRunner/IdempotencyProofRunner.csproj -- http://localhost:5203 tests/idempotency-proof.txt "Host=localhost;Port=5432;Database=order_db;Username=postgres;Password=postgres"
```

Each run writes:
- `tests/idempotency-proof.txt` (latest)
- `tests/idempotency-proof-YYYYMMDD-HHMMSS.txt` (archived)

## Video Walkthrough
Add your video link here:
- `<PASTE-LOOM-OR-YOUTUBE-LINK>`
