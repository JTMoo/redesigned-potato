# Architecture & Design Decisions

## Agreed Foundation

### Infrastructure & Deployment
- **Local Orchestration**: Docker Compose for development and local testing
- **Frontend**: React (Vite) — enforces true API decoupling, works for web and PWA mobile access, enables React Native for future native mobile apps

### Observability & Operations
- **Logging**: Seq (single Docker container, browser UI at `localhost:5341`)
- **API Documentation**: Swagger/OpenAPI — generated from code, kept in sync with implementation
- **Testing Strategy**:
  - Unit tests per service
  - Integration tests for API contracts
  - E2E tests for critical workflows

### Developer Experience
- **Single command startup**: `docker-compose up` spins up the entire stack
- **Documentation**: Setup instructions, service responsibilities, API contract definitions

---

## Frontend Strategy: React

### Decision: React (Vite)

**Why React instead of Blazor:**

1. **PWA for camera access**: Receipt scanning requires mobile camera. React PWA accesses camera via browser Web APIs (`getUserMedia`, `<input type="file" capture>`).

2. **True native mobile expansion**: The project explicitly targets potential React Native implementation.
   - React web + React Native share JavaScript logic (validation, utilities, API calls)
   - Blazor (C#) would require a separate mobile implementation (Xamarin/MAUI)

3. **Ecosystem for expense tracking**: More packages for charts, receipt parsing, offline sync in the JavaScript ecosystem.

**Implementation approach:**
- **Phase 1-2 (Web + PWA)**: React web app with PWA manifest for camera access on mobile browsers
- **Phase 3+ (Native mobile)**: React Native shares API client code, validation logic. UI layer is separate.

---

## Detailed Discussion: API Gateway + Domain Services Pattern

### Architecture Overview

Three layers:

1. **API Gateway** (single entry point)
   - YARP (Yet Another Reverse Proxy) — Microsoft's official .NET reverse proxy library
   - Handles Google OAuth + JWT issuance and validation
   - Forwards verified `X-User-Id` header to downstream services
   - Routes requests to appropriate domain services
   - Single public interface, hides service topology from clients

2. **Domain Services** (one per domain)
   - Each owns a specific business domain, data model, and database
   - Exposes REST endpoints
   - Communicates with other services via async events (RabbitMQ)
   - Trusts `X-User-Id` header forwarded from gateway

3. **Event Bus** (asynchronous communication backbone)
   - RabbitMQ + MassTransit (.NET)
   - Services publish domain events when something happens
   - Other services subscribe to events they care about

### Auth Architecture

**Gateway-level JWT validation:**

1. React redirects user to Google OAuth
2. Google returns auth code to React app callback
3. React sends code to API Gateway `/auth/callback`
4. Gateway exchanges code for Google token, fetches user info, creates/finds user via User Service
5. Gateway issues its own JWT (signed with `JWT_SECRET`)
6. All subsequent requests: React sends `Authorization: Bearer <jwt>`
7. Gateway validates JWT, extracts `userId`, injects `X-User-Id` header downstream
8. Downstream services trust `X-User-Id` — they do not re-validate JWT

### Service Boundaries

- **User Service**: Authentication (Google OAuth), profiles, preferences, location settings
- **Receipt Service**: Receipt upload/storage, OCR processing (Tesseract), item extraction, purchase history
- **Deal Service**: Deal catalog, manual deal entry, deal metadata (pricing, validity, location)
- **Matching Service**: Matches purchases to deals, calculates savings potential, generates recommendations
- **Aggregation Service**: External deal source integration (API polling, web scraping, flyer parsing)
- **Notification Service**: Deal alerts, purchase reminders, savings notifications

### Consistency Model

**Eventual consistency** — no ACID transactions across services.

Example: When a deal is created:
1. Deal Service saves to its database, publishes `DealCreatedEvent`
2. Matching Service receives event, runs matching logic
3. Notification Service receives downstream event, sends notifications

Requires:
- Idempotent event handlers (processing same event twice is safe)
- Retry logic with exponential backoff (handled by MassTransit)
- Dead-letter queues for failed events

### Data Ownership

Each service owns its tables. Zero cross-service foreign keys.

```
User Service DB:
  - users
  - user_preferences

Receipt Service DB:
  - receipts
  - receipt_items

Deal Service DB:
  - deals
  (location_zip is nullable — null = national/online deal)

Matching Service DB:
  - purchase_deal_matches
  - recommendation_cache

Aggregation Service DB:
  - deal_sources
  - scrape_jobs

Notification Service DB:
  - user_subscriptions
  - notification_log
```

Cross-service data access: call the owning service's API. No shared databases.

---

## Event-Based Communication

### Technology: RabbitMQ + MassTransit

- RabbitMQ: message broker (queues, topics, dead-letter queues, durable)
- MassTransit: .NET abstraction layer — handles serialization, retries, consumer registration

### Event Catalog

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

### Event Design Principles

1. **Events are facts, immutable** — past tense names (`DealCreated`, not `CreateDeal`)
2. **Include all data needed by subscribers** — avoid forcing subscribers to call back
3. **Versioning matters** — events are contracts; old consumers must handle new versions
4. **Avoid event chains** — if B needs to trigger C, do it synchronously in B

### Example Event Flow: Receipt Scanned

```
1. User uploads receipt via React app
2. React → API Gateway → Receipt Service
3. Receipt Service extracts items via Tesseract OCR, stores in DB
4. Receipt Service publishes ReceiptCreatedEvent + ItemsExtractedEvent

5. Matching Service receives events:
   - Looks up current deals for those items
   - Calculates savings potential
   - Publishes PotentialSavingsFoundEvent

6. Notification Service receives PotentialSavingsFoundEvent:
   - Checks user notification preferences
   - Sends "You could save €X if you buy X at Y deal"
```

---

## Database Strategy: Separate PostgreSQL Per Service

### Why Separate Databases

- Prevents tight coupling at data layer
- Forces good service boundaries
- Enables independent schema evolution
- Services scale independently

### Local Development (Docker Compose)

```yaml
postgres-users:
  image: postgres:15
  environment:
    POSTGRES_DB: users_db

postgres-receipts:
  image: postgres:15
  environment:
    POSTGRES_DB: receipts_db

postgres-deals:
  image: postgres:15
  environment:
    POSTGRES_DB: deals_db

postgres-matching:
  image: postgres:15
  environment:
    POSTGRES_DB: matching_db

postgres-aggregation:
  image: postgres:15
  environment:
    POSTGRES_DB: aggregation_db

postgres-notifications:
  image: postgres:15
  environment:
    POSTGRES_DB: notifications_db
```

---

## Implementation Roadmap

### Milestone 1 (MVP — Infrastructure Scaffold)
- Set up Docker Compose with all services + RabbitMQ + Seq
- Scaffold all 6 service projects with full layer structure (entities, DbContext, migrations, events, /health + CRUD stubs)
- API Gateway with YARP routing + Google OAuth + JWT
- React frontend with Vite, Google OAuth redirect, protected route

### Milestone 2 (Core Domain — Receipts)
- Receipt Service: Upload, Tesseract OCR, item extraction (full logic)
- React app: Receipt upload UI with camera access
- Events: `ReceiptCreatedEvent`, `ItemsExtractedEvent` flowing end-to-end

### Milestone 3 (Deal Catalog)
- Deal Service: Full CRUD for deals
- Manual deal entry UI in React
- Events: `DealCreatedEvent`, `DealUpdatedEvent` flowing end-to-end

### Milestone 4 (Matching & Recommendations)
- Matching Service: Full match logic, savings calculation
- Notification Service: Alert delivery
- Events: `PotentialSavingsFoundEvent` flowing end-to-end

### Milestone 5 (Aggregation)
- Aggregation Service: External deal source integration
- Scraper/API poller for Kaufda, retailer sites
- Feeds new deals into Deal Service

### Milestone 6 (Polish)
- Integration tests per service
- E2E tests for critical flows
- Performance optimization
- API documentation complete
