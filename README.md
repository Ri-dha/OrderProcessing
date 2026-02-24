# Order Processing Service

## Tech Stack
- .NET 10
- ASP.NET Core Web API
- Wolverine
- EF Core
- PostgreSQL

## Run with Docker
```bash
docker compose -f docker-compose.yml up --build
```

API base URL: `http://localhost:8080`

## Key Endpoints
- `POST /api/products`
- `POST /api/products/bulk`
- `GET /api/products`
- `PUT /api/products/{id}`
- `POST /api/orders`
- `POST /api/orders/{id}/confirm`
- `POST /api/orders/{id}/pay/initiate`
- `POST /api/orders/{id}/pay/verify`
- `POST /api/orders/{id}/fulfill`
- `POST /api/orders/{id}/ship`
- `POST /api/orders/{id}/deliver`
- `POST /api/orders/{id}/refund/request`
- `POST /api/orders/{id}/refund/complete`
- `POST /api/orders/{id}/cancel`
- `GET /api/orders/{id}`

## Build & Test
```bash
dotnet build OrderProcessing.sln
dotnet test OrderProcessing.sln
```

## Required Proof Artifacts
- `tests/concurrency-proof.txt`
- `tests/idempotency-proof.txt`

## Concurrency Proof Runner
Run this while the API is already running locally:
```bash
dotnet run --project tests/ConcurrencyProofRunner/ConcurrencyProofRunner.csproj
```

Optional arguments:
```bash
dotnet run --project tests/ConcurrencyProofRunner/ConcurrencyProofRunner.csproj -- http://localhost:5203 tests/concurrency-proof.txt
```

Each run writes:
- `tests/concurrency-proof.txt` (latest)
- `tests/concurrency-proof-YYYYMMDD-HHMMSS.txt` (archived copy)

## Idempotency Proof Runner
Run this while the API and PostgreSQL are running:
```bash
dotnet run --project tests/IdempotencyProofRunner/IdempotencyProofRunner.csproj
```

Optional arguments:
```bash
dotnet run --project tests/IdempotencyProofRunner/IdempotencyProofRunner.csproj -- http://localhost:5203 tests/idempotency-proof.txt "Host=localhost;Port=5432;Database=order_db;Username=postgres;Password=postgres"
```

Each run writes:
- `tests/idempotency-proof.txt` (latest)
- `tests/idempotency-proof-YYYYMMDD-HHMMSS.txt` (archived copy)

## Video Walkthrough
Add your video link here:
- `<PASTE-LOOM-OR-YOUTUBE-LINK>`
