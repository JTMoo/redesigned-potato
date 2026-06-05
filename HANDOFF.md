# Bonveo — Agent Handoff Document

**Created:** 2026-06-05  
**Status of working tree:** ~146 files with uncommitted changes against the last commit (`d72a895`)

---

## What this project is

**Bonveo** (formerly "Expense Tracker") is a German-market receipt-scanning and deal-matching SaaS. Users photograph receipts; the system extracts line items via OCR, matches them against a deal catalogue, and notifies users of potential savings. The domain is `bonveo.de`.

Stack: .NET 10 microservices + React (Vite) frontend + Docker Compose for the full local stack.

---

## Repository layout

```
redesigned-potato/
├── services/
│   ├── api-gateway/          # YARP proxy; Keycloak JWT validation; X-User-Id injection
│   ├── user-service/         # User profiles and preferences (port 8081)
│   ├── receipt-service/      # Upload + MinIO storage + OCR stub (port 8082)
│   ├── deal-service/         # Deal CRUD (port 8083)
│   ├── matching-service/     # ItemsExtracted consumer; matching engine (port 8084)
│   ├── aggregation-service/  # Spending summaries — stub only (port 8085)
│   └── notification-service/ # PotentialSavings consumer; in-app notifs (port 8086)
├── shared/
│   ├── contracts/            # All MassTransit event records (shared across services)
│   └── utilities/            # IDateTimeProvider, UserIdMiddleware
├── frontend/react-app/       # React + oidc-client-ts; Keycloak PKCE flow
├── keycloak/                 # realm-export.json (Google IdP configured)
├── docs/adr/                 # Architecture Decision Records (ADR 0001–0005)
├── tech_debt.md              # Known deferred items
├── CLAUDE.md                 # Coding standards — read this before touching anything
├── CONTEXT.md                # Domain glossary and design decisions
├── MVP_PLAN.md               # Wave 1 scaffold plan (completed)
├── WAVE2_PLAN.md             # Wave 2 vertical slice plan (completed, see review below)
└── WAVE2_REVIEW.md           # Senior Dev review of Wave 2 — critical issues listed
```

---

## Auth architecture (ADR 0001 — Keycloak)

**This is the most important architectural context.** The project migrated from Google OAuth + custom JWT (Wave 2) to **Keycloak** as the identity provider (post-Wave-2, still uncommitted).

- Keycloak runs in Docker and handles all credential storage, Google OAuth, and email/password auth
- The api-gateway validates Keycloak-issued JWTs via JWKS auto-discovery (no longer issues its own tokens)
- The frontend uses `oidc-client-ts` with Authorization Code + PKCE — **not** the Keycloak JS adapter
- Downstream services trust the `X-User-Id` header injected by the api-gateway; they never touch auth
- Plan tier (`free` / `premium`) is stored as a Keycloak realm role and included as a JWT claim
- Apple Sign-In is **deferred** — requires a custom Keycloak SPI extension (see `tech_debt.md`)

---

## Current build and test status

As of 2026-06-05:

| Metric | Status |
|--------|--------|
| `dotnet build expense-tracker.sln -c Release` | **Passes — 0 errors, 0 warnings** |
| `dotnet test expense-tracker.sln -c Release --no-build` | **140 tests, all passing** |

Test breakdown:
- `api-gateway.Tests`: 4 passed
- `aggregation-service.Tests`: 1 passed
- `deal-service.Tests`: 21 passed
- `user-service.Tests`: 30 passed
- `receipt-service.Tests`: 52 passed
- `matching-service.Tests`: 18 passed
- `notification-service.Tests`: 14 passed

---

## Uncommitted working tree — what changed

There are **~89 changed files, ~2046 insertions, ~1356 deletions** that have never been committed. These represent the Keycloak migration and several post-Wave-2 fixes. Key changes:

### api-gateway
- `AuthController.cs` and `JwtService.cs` **deleted** — replaced by Keycloak JWKS validation in `Program.cs`
- `Program.cs` now uses `AddJwtBearer` with `options.Authority = Keycloak:Authority` and a middleware that injects `X-User-Id` from the `sub` claim and publishes `UserCreatedEvent` on first login
- `JwtServiceTests.cs` deleted (class no longer exists)

### frontend
- `LoginPage.tsx` deleted — login is now Keycloak-hosted
- `AuthContext.tsx` rewired to `oidc-client-ts` PKCE flow
- `OAuthCallback.tsx` handles the Keycloak redirect
- `ReceiptDetail.tsx` and `ReceiptList.tsx` significantly expanded (341 and 217 lines added)

### matching-service
- `MatchesController.cs` deleted (stub replaced with `Presentation/` layer)
- `MatchingEngine.cs` simplified to a pure static function
- Tests substantially refactored

### notification-service
- DB migration snapshot updated
- Use cases and tests refined

### user-service
- Test suite significantly expanded (+208 lines in `UseCaseTests.cs`, +140 in controller tests)

### docker-compose.yml
- Keycloak service + its own PostgreSQL added
- Infrastructure changes to support the new auth flow

---

## Architecture Decision Records (ADRs)

| ADR | Decision | Status |
|-----|----------|--------|
| 0001 | Keycloak as identity provider | Accepted |
| 0002 | Brand name: Bonveo | Accepted |
| 0002 | Single PostgreSQL server (separate from multi-DB ADR) | Accepted |
| 0003 | Shadcn/UI + Tailwind CSS component system | Accepted — **not yet implemented** |
| 0004 | PostHog self-hosted analytics | Accepted — **not yet implemented** |
| 0005 | Anthropic API as premium OCR (Claude Vision) | Accepted — **not yet implemented** |

---

## Known issues from WAVE2_REVIEW.md (status as of handoff)

The review was done before the Keycloak migration. Many critical issues have been addressed in the uncommitted working tree. Items to verify:

### Likely fixed (in uncommitted changes)
- Critical #1: Real Google OAuth secret and JWT secret in `.env` — the JWT/OAuth arch is replaced by Keycloak; credentials are now Keycloak realm secrets
- Critical #2: RabbitMQ config key mismatch (`RabbitMq__` vs `RabbitMQ__`) — check `appsettings.Development.json` files
- Critical #3: Receipt controller clean architecture violation — `UploadReceiptUseCase` exists
- Major #4: `continue-on-error: true` in CI — check `.github/workflows/build-and-test.yml`
- Major #5: Unbounded list queries — pagination added per commit `68361ce`

### Still open (not yet addressed)
- **ADR 0003 not implemented**: Frontend still uses handwritten CSS and no Shadcn/Tailwind
- **ADR 0004 not implemented**: PostHog analytics container not in docker-compose.yml
- **ADR 0005 not implemented**: OCR is still the stub (`TesseractOcrService` returns hardcoded items); real Anthropic Claude Vision integration not built
- **Aggregation service**: Still a stub — no business logic, only 1 test (smoke test)
- **CORS**: No `AddCors`/`UseCors` in any service
- **No `.dockerignore`** file — bin/obj/Tests are copied into build context
- **MassTransit retry policy** — no `UseMessageRetry` on any consumer
- **Missing API health checks in docker-compose** — app services have no `healthcheck:` stanzas
- **`SavingOpportunityEvent`** declared in shared contracts but unused anywhere
- **Apple Sign-In** — requires custom Keycloak SPI extension (see `tech_debt.md`)
- **Keycloak custom theme** — stock Keycloak UI; Bonveo brand theme not applied (see `tech_debt.md`)
- **`FRONTEND_URL` hardcoded** in docker-compose instead of using `${FRONTEND_URL:-http://localhost:3000}`
- **floating Docker image tags** — `datalust/seq:latest`, `minio/minio:latest`, `nginx:alpine`

---

## Logical next steps (priority order)

### Step 1 — Commit the Keycloak migration (urgent)

The ~89-file working tree has passing builds and tests. The uncommitted changes represent real, verified work. Commit everything to `main` before starting new work.

```bash
git add -A   # review staged files carefully first
git commit -m "feat(auth): migrate to Keycloak; remove custom JWT; rewire frontend to oidc-client-ts"
```

### Step 2 — Implement ADR 0003: Shadcn/UI + Tailwind migration

The frontend currently uses `global.css` with handwritten styles. The ADR mandates Shadcn/UI on Tailwind:
1. Add Tailwind + PostCSS to `frontend/react-app/`
2. Configure `tailwind.config` with Bonveo design tokens (green + amber palette, per memory)
3. Migrate existing pages (Dashboard, ReceiptList, ReceiptDetail, Notifications, ReceiptUpload) to Tailwind utility classes
4. Add first Shadcn components (Button, Card, Badge, Table, NavBar)
5. Remove `global.css` and `LandingPage.css`

### Step 3 — Implement ADR 0005: Real Claude Vision OCR (premium tier)

The stub `TesseractOcrService` returns 3 hardcoded items. Real implementation:
1. Add `Anthropic` NuGet package (or HTTP client) to `receipt-service`
2. Create `ClaudeVisionOcrService : IOcrService` in `receipt-service/src/Infrastructure/Ocr/`
3. Implement receipt image → base64 → Claude Vision prompt → parse `ScannedReceipt`
4. Register both services; gate on user's plan tier (free → stub, premium → Claude Vision)
5. **Prerequisite**: Sign Anthropic DPA before going live (see `docs/legal/anthropic-dpa-checklist.md`)

### Step 4 — Implement ADR 0004: PostHog analytics

Add PostHog self-hosted to Docker Compose and instrument key frontend events:
1. Add `posthog/posthog-ce` container to `docker-compose.yml`
2. Install `posthog-js` in the React frontend
3. Instrument: `receipt_uploaded`, `deal_match_viewed`, `notification_read`, `signup_completed`
4. Cookieless config (no consent banner required at launch)

### Step 5 — Aggregation service business logic

`aggregation-service` is a pure stub with 1 smoke test. Implement:
1. Consume `PotentialSavingsFoundEvent` and aggregate per-user totals
2. `GET /aggregations/me` → returns total savings, savings by category/period
3. Wire into the Dashboard frontend to show spending summary

### Step 6 — Production hardening (before any public launch)

- Add CORS to all services, scoped to `FRONTEND_URL`
- Add `.dockerignore` at repo root
- Pin all floating Docker image tags (`seq`, `minio`, `nginx`)
- Add `healthcheck:` stanzas to all app services in docker-compose
- Add MassTransit retry policy to all consumers
- Remove `SavingOpportunityEvent` from shared contracts or wire it up
- Add `healthcheck:` stanza to api-gateway in docker-compose
- Replace `FRONTEND_URL: http://localhost:3000` with `${FRONTEND_URL:-http://localhost:3000}`

---

## Key conventions (from CLAUDE.md — mandatory)

- **Nullable reference types enabled** in all `.csproj` files — no `#pragma warning disable`
- **`ArgumentNullException.ThrowIfNull()`** on all non-nullable method arguments, at method entry
- **Clean Architecture**: controllers are thin; all business logic lives in use cases under `Application/UseCases/`
- **No service-locator pattern** — constructor injection only
- **Tests**: 80%+ coverage; Arrange-Act-Assert; xUnit + Moq + FluentAssertions
- **Build verification**: `dotnet build expense-tracker.sln -c Release` must be 0 errors, 0 warnings before commit
- **Test verification**: `dotnet test expense-tracker.sln -c Release --no-build` — 0 failures before commit
- **Use case naming**: `Find*UseCase` = write path (consumer-triggered); `Get*UseCase` = read path (controller-triggered)

---

## Service port map

| Service | Port |
|---------|------|
| Frontend (nginx) | 3000 |
| API Gateway | 8080 |
| User Service | 8081 |
| Receipt Service | 8082 |
| Deal Service | 8083 |
| Matching Service | 8084 |
| Aggregation Service | 8085 |
| Notification Service | 8086 |
| Keycloak | 8443 |
| RabbitMQ Management | 15672 |
| Seq | 5341 |
| MinIO API | 9000 |
| MinIO Console | 9001 |

---

## Event flow (implemented)

```
User uploads receipt
  → receipt-service stores image (MinIO), runs OCR stub
  → publishes ItemsExtractedEvent (RabbitMQ)
  → matching-service consumes, runs MatchingEngine, persists PurchaseDealMatch
  → publishes PotentialSavingsFoundEvent
  → notification-service consumes, creates NotificationLog
  → frontend polls GET /api/notifications
```

`DealCreatedEvent`, `DealUpdatedEvent`, `DealArchivedEvent` are published by deal-service but matching-service caches deals via the `RecommendationCache` entity (populated on `DealCreated`/`DealUpdated`, invalidated on `DealArchived`).

---

## Files to read first when starting work

1. `CLAUDE.md` — mandatory conventions
2. `CONTEXT.md` — domain glossary and design decisions
3. `docs/adr/` — all ADRs, especially 0001 (Keycloak) and whichever feature you're implementing
4. `WAVE2_REVIEW.md` — still-open issues to avoid reintroducing
5. `tech_debt.md` — deferred items with clear "when to revisit" notes
