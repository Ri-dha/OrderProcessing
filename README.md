# Order Processing Service

## Tech Stack
- .NET 10
- ASP.NET Core Web API
- Wolverine
- EF Core
- PostgreSQL

## Run with Docker
```bash
docker compose -f compose.yaml up --build
```

API base URL: `http://localhost:8080`

## Key Endpoints
- `POST /api/products`
- `POST /api/orders`
- `POST /api/orders/{id}/confirm`
- `POST /api/orders/{id}/pay` (requires `Idempotency-Key` header)
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

## Video Walkthrough
Add your video link here:
- `<PASTE-LOOM-OR-YOUTUBE-LINK>`
