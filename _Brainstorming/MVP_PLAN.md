# MVP Plan: Infrastructure Scaffold

## Resolved Decisions

| Topic | Decision |
|---|---|
| Starting point | Start fresh — ignore the existing Razor Pages app |
| .NET version | .NET 10 / C# 14 |
| Frontend | React (Vite) |
| API Gateway | YARP (ASP.NET Core) |
| Auth | Google OAuth + JWT, from day one |
| OCR | Tesseract (local, free); switchable via factory pattern |
| Event bus | RabbitMQ + MassTransit |
| Logging | Seq (single Docker container, browser UI at localhost:5341) |
| Shared contracts | Shared C# class library referenced by all services (monorepo project ref) |
| Deal.LocationZip | Nullable — `null` = national/online deal |
| DB per service | Separate PostgreSQL instance per service |
| Data access | EF Core code-first, no repository pattern, DbContext injected directly |
| DI container | Microsoft.Extensions.DependencyInjection (built-in, no third-party) |

---

## MVP Scope: Infrastructure Scaffold

**Goal:** All 6 services + API Gateway + React frontend fully scaffolded and running via `docker-compose up`. No complete business logic — stubs are fine — but all layers must be present and wired.

**Done means:**
- `docker-compose up` starts the entire stack without errors
- Every service's `/health` endpoint returns 200
- RabbitMQ queues are created on startup
- EF migrations run automatically on startup
- Seq receives structured logs from all services
- Google OAuth redirect flow works in the React app (redirects to Google, callback handled)

---

## Repo Structure

```
expense-tracker/
├── docker-compose.yml              ← all infra + services + frontend
├── docker-compose.dev.yml          ← hot-reload volume mounts
├── .env.example                    ← all env var names documented, no secrets
├── .gitignore
├── README.md
│
├── shared/
│   ├── contracts/                  ← all event records (referenced by services)
│   │   ├── EventContracts.csproj
│   │   └── Events/
│   │       ├── UserCreatedEvent.cs
│   │       ├── ReceiptCreatedEvent.cs
│   │       ├── ItemsExtractedEvent.cs
│   │       ├── DealCreatedEvent.cs
│   │       ├── DealUpdatedEvent.cs
│   │       ├── DealArchivedEvent.cs
│   │       ├── PotentialSavingsFoundEvent.cs
│   │       └── SavingOpportunityEvent.cs
│   └── utilities/
│       ├── Utilities.csproj
│       └── IDateTimeProvider.cs
│
├── services/
│   ├── api-gateway/                ← YARP + Google OAuth + JWT forwarding
│   ├── user-service/
│   ├── receipt-service/
│   ├── deal-service/
│   ├── matching-service/
│   ├── aggregation-service/
│   └── notification-service/
│
└── frontend/
    └── react-app/                  ← Vite + Google OAuth + protected routes
```

### Service Internal Structure (each service)

```
service-name/
├── Dockerfile
├── service-name.csproj             ← references shared/contracts + shared/utilities
├── src/
│   ├── Domain/                     ← entity models (code-first)
│   ├── Data/                       ← DbContext with OnModelCreating config
│   ├── Features/                   ← stubbed command/query handlers
│   ├── Events/                     ← MassTransit consumer classes
│   ├── Controllers/                ← /health + basic CRUD stubs
│   └── Infrastructure/
│       └── ServiceCollectionExtensions.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
└── Tests/
    └── service-name.Tests.csproj   ← xUnit skeleton
```

---

## Multi-Agent Implementation Plan

### Wave 1 — Parallel (no dependencies)

| Agent | Responsibility | Key outputs |
|---|---|---|
| **A: Foundation** | Repo skeleton + Docker Compose | `docker-compose.yml`, `docker-compose.dev.yml`, `.env.example`, `.gitignore`, `README.md` |
| **B: Shared projects** | C# shared libraries | All 8 event records in `shared/contracts/`; `IDateTimeProvider` in `shared/utilities/` |
| **I: React frontend** | Vite scaffold + OAuth | Google OAuth redirect, protected route, API client stub, Dockerfile |

### Wave 2 — Parallel (depends on Agent B completing first)

| Agent | Responsibility | Key entities / events |
|---|---|---|
| **C: API Gateway** | YARP routing + Google OAuth | Routes to all 6 services; JWT validation; `X-User-Id` header forwarded downstream |
| **D: User Service** | User management | `User`, `UserPreference`; publishes `UserCreatedEvent` |
| **E: Receipt Service** | Receipt upload + OCR wiring | `Receipt`, `ReceiptItem`; `IOcrService` + `TesseractOcrService`; publishes `ReceiptCreatedEvent`, `ItemsExtractedEvent` |
| **F: Deal Service** | Deal catalog | `Deal` (nullable `LocationZip`); CRUD; publishes `DealCreatedEvent`, `DealUpdatedEvent`, `DealArchivedEvent` |
| **G: Matching Service** | Purchase-to-deal matching | `PurchaseDealMatch`, `RecommendationCache`; consumes `ReceiptCreatedEvent` + `DealCreatedEvent`; publishes `PotentialSavingsFoundEvent` |
| **H: Aggregation + Notification** | Two lighter services | Aggregation: `DealSource`, `ScrapeJob`; Notification: `UserSubscription`, `NotificationLog`; consumes `PotentialSavingsFoundEvent` |

### Wave 3 — Sequential (integration + verification)

One agent assembles everything:
- Completes `docker-compose.yml` with all service wiring
- Verifies `docker-compose up --build` succeeds
- Confirms all `/health` endpoints return 200
- Confirms RabbitMQ queues appear in management UI
- Confirms Seq receives logs

---

## Auth Architecture Decision

**Gateway-level JWT validation** (simpler, recommended for MVP):

1. React app redirects user to Google OAuth
2. Google returns an authorization code to the React app callback
3. React sends the code to the API Gateway's `/auth/callback` endpoint
4. Gateway exchanges code for Google access token, fetches user info, creates/finds user via User Service
5. Gateway issues its own JWT (signed with `JWT_SECRET`)
6. All subsequent requests: React sends `Authorization: Bearer <jwt>`
7. Gateway validates JWT, extracts `userId`, forwards `X-User-Id: <id>` header to downstream services
8. Downstream services **trust** `X-User-Id` — they do not re-validate the JWT

This means services are not individually secured. They rely on the gateway being the only entry point.

---

## Open Questions (not yet answered)

1. **Google OAuth credentials**: Do you already have a Client ID + Secret, or should `.env.example` just have placeholder values?

2. **Matching stub depth**: For the scaffold, should Matching Service return "no matches" always, or implement basic `string.Contains()` matching as a starting point?

3. **Receipt image storage**: Local Docker volume (simplest) vs. MinIO (S3-compatible, adds a container)?

4. **GitHub Actions**: Should the scaffold include `.github/workflows/` CI pipelines per service, or leave CI for later?

---

## Event Catalog

| Event | Published by | Consumed by |
|---|---|---|
| `UserCreatedEvent` | User Service | Notification Service |
| `ReceiptCreatedEvent` | Receipt Service | Matching Service |
| `ItemsExtractedEvent` | Receipt Service | Matching Service |
| `DealCreatedEvent` | Deal Service | Matching Service |
| `DealUpdatedEvent` | Deal Service | Matching Service |
| `DealArchivedEvent` | Deal Service | Matching Service |
| `PotentialSavingsFoundEvent` | Matching Service | Notification Service |
| `SavingOpportunityEvent` | Matching Service | Notification Service |
