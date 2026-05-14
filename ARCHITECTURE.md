# Architecture & Design Decisions

## Agreed Foundation

### Infrastructure & Deployment
- **Local Orchestration**: Docker Compose for development and local testing
- **Frontend**: React - enforces true API decoupling, works for web and PWA mobile access, enables React Native for future native mobile apps

### Observability & Operations
- **Logging**: Seq or ELK stack (local deployment)
- **API Documentation**: Swagger/OpenAPI - generated from code, kept in sync with implementation
- **Testing Strategy**: 
  - Unit tests per service
  - Integration tests for API contracts
  - E2E tests for critical workflows

### Developer Experience
- **Single command startup**: `docker-compose up` should spin up entire stack
- **Documentation**: Setup instructions, service responsibilities, API contract definitions

---

## Frontend Strategy: React vs Blazor for Mobile

### Decision: React

**Why React instead of Blazor:**

1. **PWA for camera access**: Receipt scanning requires mobile camera. Both React and Blazor can do PWA with camera access via Web APIs. No advantage either way.

2. **True native mobile expansion**: Your project explicitly mentions potential React Native implementation. React has advantages here:
   - React web + React Native share JavaScript logic (validation, utilities, API calls)
   - Blazor (C#) would require separate mobile implementation (Xamarin/MAUI - different ecosystem)

3. **Ecosystem for expense tracking**: More packages exist for charts, receipt parsing, offline sync in JavaScript ecosystem vs C#

**Implementation approach:**

- **Phase 1-2 (Web + PWA)**: React web app with PWA manifest for camera access on mobile browsers
- **Phase 3+ (Native mobile)**: React Native shares API client code, validation logic. UI layer is separate.

**Key insight**: The API decoupling is what matters. React, Blazor, Flutter, or SwiftUI all work equally well as long as they call your REST API. The frontend choice only matters when you expand to native mobile - then React → React Native is a natural progression.

---

## Detailed Discussion: API Gateway + Domain Services Pattern

### Architecture Overview

The pattern consists of three layers:

1. **API Gateway** (single entry point)
   - Routes requests to appropriate domain services
   - Handles cross-cutting concerns: authentication, rate limiting, request/response transformation
   - Single public interface, hides service topology from clients

2. **Domain Services** (as many as needed, one per domain)
   - Each owns a specific business domain
   - Owns its data model and database
   - Exposes REST or gRPC endpoints
   - Communicates with other services via async events (event bus)
   - Service count follows domain complexity, not arbitrary constraints

3. **Event Bus** (asynchronous communication backbone)
   - Services publish domain events when something happens
   - Other services subscribe to events they care about
   - Loosely coupled, asynchronous by default

### Architectural Decisions This Implies

#### Service Boundaries (Expense Tracker Domains)

For your expense tracker project, natural domain boundaries are:

- **User Service**: Authentication (OAuth), profiles, preferences, location settings
- **Receipt Service**: Receipt upload/storage, OCR processing, item extraction, purchase history
- **Deal Service**: Deal catalog, manual deal entry, deal metadata (pricing, validity, location)
- **Matching Service**: Matches purchases to deals, calculates savings potential, generates recommendations
- **Aggregation Service**: External deal source integration (API polling, web scraping, flyer parsing)
- **Notification Service**: Deal alerts, purchase reminders, savings notifications

That's 6 services. This is correct. The "2-3 service guideline" was just a learning example - split by actual domain boundaries, not arbitrary counts.

Clean boundaries mean:
- Receipt Service doesn't own deals (only references deal IDs)
- Matching Service doesn't duplicate data (reads from Receipt and Deal services)
- Changes to Aggregation scraper don't affect Notification logic
- Each service can be deployed independently when its domain changes

#### Consistency Model
**Eventual consistency**, not ACID transactions across services.

Example: When a deal is created:
1. Deal Service saves to its database, publishes `DealCreated` event
2. Notification Service receives event, sends notifications
3. Search Service receives event, updates search index

The system is temporarily inconsistent (notifications might lag), but converges to consistent state. This requires:
- Idempotent event handlers (processing same event twice is safe)
- Retry logic with exponential backoff
- Dead-letter queues for failed events

#### Data Ownership (Expense Tracker Example)
Each service owns its tables. Zero cross-service foreign keys.

```
User Service DB:
  - users
  - user_preferences
  - user_locations

Receipt Service DB:
  - receipts
  - receipt_items
  - purchase_history

Deal Service DB:
  - deals
  - deal_validity_periods
  - deal_locations
  (never queries user or receipt tables)

Matching Service DB:
  - purchase_deal_matches (denormalized view of matches)
  - recommendation_cache (temporary for performance)

Aggregation Service DB:
  - deal_sources
  - scrape_jobs (logs of when scrapes happened)
  - deal_sync_status

Notification Service DB:
  - user_subscriptions
  - notification_log
```

If Matching Service needs purchase data, it calls Receipt Service API (fresh data). If it needs user location for deal filtering, it either:
- Calls User Service synchronously (for fresh location)
- Caches location and refreshes on user-service events
- Gets location from the deal itself (already stored in Deal Service DB)

### CI/CD Implications

Each service has independent deployment:

```
services/
  api-gateway/
    Dockerfile
    .github/workflows/deploy.yml
  user-service/
    Dockerfile
    .github/workflows/deploy.yml
  receipt-service/
    Dockerfile
    .github/workflows/deploy.yml
  deal-service/
    Dockerfile
    .github/workflows/deploy.yml
  matching-service/
    Dockerfile
    .github/workflows/deploy.yml
  aggregation-service/
    Dockerfile
    .github/workflows/deploy.yml
  notification-service/
    Dockerfile
    .github/workflows/deploy.yml
```

**Pipeline per service** (GitHub Actions example):
1. On push to `main` in `user-service/` folder
2. Run tests in `user-service/`
3. Build Docker image
4. Deploy to local registry or environment
5. Run integration tests (against other services)

**API Gateway CI/CD is special:**
- Gateway version pins which service versions it's compatible with
- Gateway config maps routes to services: `api-gateway/routes.yml`
- Breaking changes in a service require gateway update

### Deployment Coordination

For local development: `docker-compose.yml` pins all image versions together.

```yaml
services:
  # Message broker
  rabbitmq:
    image: rabbitmq:3.12-management
    ports:
      - "5672:5672"
    - "15672:15672"  # Management UI

  # Databases
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

  # Services
  api-gateway:
    image: api-gateway:v1.0
    ports:
      - "5000:5000"
    depends_on:
      - rabbitmq

  user-service:
    image: user-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-users:5432/users_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  receipt-service:
    image: receipt-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-receipts:5432/receipts_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  deal-service:
    image: deal-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-deals:5432/deals_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  matching-service:
    image: matching-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-matching:5432/matching_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  aggregation-service:
    image: aggregation-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-aggregation:5432/aggregation_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  notification-service:
    image: notification-service:v1.0
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-notifications:5432/notifications_db
      RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672

  # Frontend
  react-app:
    image: react-app:v1.0
    ports:
      - "3000:3000"
```

You update compose file when services are compatible with each other. This teaches: **deployment is coordination, not magic**.

---

## Database Strategy: Separate Databases Per Service

### Why Separate Databases

**Prevents tight coupling at data layer.** If all services share a database:
- Schema change in one service affects others
- Shared schema becomes dumping ground for all data
- Can't scale services independently (different data growth rates)
- Database becomes single point of contention

**Forces good service boundaries.** With separate databases, you immediately see: "Wait, Deal Service needs user data - how do we get it?" This friction is *good* - it clarifies service contracts.

### Trade-offs

**Costs:**
- More database instances (Docker takes care of this locally)
- Query joins across databases are impossible (forces service calls)
- Debugging is harder (data is scattered)

**Benefits:**
- Schema evolution is independent
- Services scale independently
- Clear ownership: "Deal Service owns deals table"

### Implementation

```
docker-compose.yml:
  postgres-users:
    image: postgres:15
    environment:
      POSTGRES_DB: users_db
    volumes:
      - users-data:/var/lib/postgresql/data

  postgres-deals:
    image: postgres:15
    environment:
      POSTGRES_DB: deals_db
    volumes:
      - deals-data:/var/lib/postgresql/data

  user-service:
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-users:5432/users_db

  deal-service:
    environment:
      DATABASE_URL: postgres://postgres:password@postgres-deals:5432/deals_db
```

Each service connects to its own instance via `DATABASE_URL` env var.

### Query Across Services

You can't do SQL joins. Instead (C# 14):

```csharp
// In Deal Service (primary constructor)
public class DealQueryService(IDealRepository dealRepo, IUserServiceClient userClient)
{
    public async Task<DealWithUserDto> GetDealWithUserAsync(int dealId)
    {
        var deal = await dealRepo.GetDealAsync(dealId);
        
        // Call User Service for user details
        var user = await userClient.GetUserAsync(deal.CreatedByUserId);
        
        return new DealWithUserDto(deal.Id, deal.Title, user.Email);
    }
}

// Record for response
public record DealWithUserDto(int DealId, string Title, string UserEmail);
```

This teaches API design and service communication.

---

## Event-Based Communication

### Why Event-Based

With multiple services, synchronous HTTP calls create problems:
- Service A calls Service B, B is slow → A hangs
- Service B goes down → A fails
- Hard to implement retry logic at scale

Events solve this: publish and forget. Receivers handle at their own pace.

### Open Source Options

#### 1. **RabbitMQ** (Recommended for learning)
- Message broker with queues and topics
- Durable (survives restarts)
- Dead-letter queues for failed messages
- Simple to run in Docker
- Lightweight, perfect for local dev

```yaml
event-bus:
  image: rabbitmq:3.12-management
  ports:
    - "5672:5672"    # AMQP
    - "15672:15672"  # Management UI
  environment:
    RABBITMQ_DEFAULT_USER: guest
    RABBITMQ_DEFAULT_PASS: guest
```

#### 2. **Apache Kafka**
- Event streaming platform (different paradigm than RabbitMQ)
- Better for: high throughput, event sourcing, long retention
- Heavier (requires ZooKeeper or KRaft mode)
- More complex to operate
- Overkill for small internal projects, but teaches scalability concepts

```yaml
kafka:
  image: confluentinc/cp-kafka:7.5.0
  depends_on:
    - zookeeper
```

#### 3. **NATS**
- Ultra-lightweight message broker
- Similar to RabbitMQ but simpler
- Fast, minimal resource usage
- Good for microservices, less mature ecosystem

#### 4. **MassTransit** (C# specific, abstraction layer)
Not a broker itself, but a library that abstracts over RabbitMQ, Kafka, etc.
- Write event handlers once
- Switch brokers by configuration
- Handles serialization, retries, sagas

### Recommendation for You

**Start with RabbitMQ + MassTransit**:

- RabbitMQ is easy to learn, reliable, standard in industry
- MassTransit is perfect for .NET, handles complexity for you
- You can swap to Kafka later (MassTransit supports it)

Example flow (C# 14):

```csharp
// Deal Service publishes event (record with immutability)
public record DealCreatedEvent(int DealId, string Title, int CreatedByUserId);

// In Deal Service
await _messageBus.PublishAsync(new DealCreatedEvent(
    deal.Id, 
    deal.Title,
    deal.CreatedByUserId
));

// Notification Service subscribes (primary constructor)
public class DealCreatedEventConsumer(INotificationService notificationService) 
    : IConsumer<DealCreatedEvent>
{
    public async Task Consume(ConsumeContext<DealCreatedEvent> context) =>
        await notificationService.SendDealNotificationAsync(
            context.Message.CreatedByUserId,
            context.Message.Title
        );
}
```

### Event Design Principles

1. **Events are facts, immutable**
   - `DealCreated`, `DealUpdated`, `DealArchived` - past tense
   - Include all data needed by subscribers (don't make them call back)

2. **Versioning matters**
   - Events are contracts between services
   - Old version consumers must handle new version events gracefully

3. **No event chains**
   - Service A publishes event, Service B consumes and publishes event, Service C consumes
   - Creates hidden dependencies, hard to debug
   - Better: if B needs to trigger C, do it synchronously in B

---

## Event Flows (Expense Tracker)

### Example 1: Receipt Scanned
```
1. User uploads receipt via React app
2. React → API Gateway → Receipt Service
3. Receipt Service extracts items via OCR, stores in DB
4. Receipt Service publishes ReceiptCreatedEvent (items, prices, store)
   
5. Matching Service receives event:
   - Looks up current deals for those items
   - Calculates savings potential
   - Publishes PotentialSavingsFoundEvent

6. Notification Service receives PotentialSavingsFoundEvent:
   - Checks user notification preferences
   - Sends "You could save €X if you buy X at Y deal"

7. Aggregation Service can also react:
   - Tracks which items users are buying (market research data)
```

### Example 2: New Deal Added
```
1. Admin or Aggregation Service adds deal to Deal Service
2. Deal Service publishes DealAddedEvent

3. Matching Service receives event:
   - Runs matching logic against all previous purchases
   - For each user with matching purchases, publishes SavingOpportunityEvent

4. Notification Service sends targeted notifications
```

---

## Implementation Roadmap

1. **Phase 1 (Infrastructure):**
   - Set up docker-compose with all services + RabbitMQ
   - Create API Gateway routing configuration
   - Scaffold all 6 service projects with basic endpoints

2. **Phase 2 (Core Domain - Receipts):**
   - Receipt Service: Upload, OCR, item extraction
   - React app: Receipt upload UI
   - Events: `ReceiptCreatedEvent`, `ItemsExtractedEvent`

3. **Phase 3 (Deal Catalog):**
   - Deal Service: CRUD for deals
   - Manual deal entry UI in React
   - Events: `DealAddedEvent`, `DealUpdatedEvent`

4. **Phase 4 (Matching & Recommendations):**
   - Matching Service: Match logic, savings calculation
   - Notification Service: Alert delivery
   - Events: `PotentialSavingsFoundEvent`

5. **Phase 5 (Aggregation):**
   - Aggregation Service: External deal source integration
   - Scraper/API poller for Kaufda, retailer sites
   - Feed new deals into Deal Service

6. **Phase 6 (Polish):**
   - Add Seq logging across all services
   - Integration tests per service
   - E2E tests for critical flows
   - Performance optimization
