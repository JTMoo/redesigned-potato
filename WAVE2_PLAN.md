# Wave 2 — Full Vertical Slice

## Goal

Implement the complete end-to-end flow:

```
User uploads receipt
  → receipt-service stores image (MinIO) + extracts items (OCR stub)
  → ItemsExtractedEvent published via RabbitMQ
  → matching-service consumes event, finds matching deals, persists matches
  → PotentialSavingsFoundEvent published
  → notification-service consumes event, creates in-app notification
  → React frontend shows receipts, matches, and notifications
```

Everything must compile, all health checks must pass, and every service must
reach at least **80% unit-test coverage** on business logic (use cases, domain
logic, consumers). Use xUnit + Moq + FluentAssertions. Follow CLAUDE.md strictly.

---

## Architecture Constraints (read before coding)

- **Clean Architecture**: Domain → Application (use cases) → Infrastructure → Presentation
- **No service-to-service HTTP** except where already established (api-gateway → user-service upsert). All cross-service communication goes through RabbitMQ via MassTransit.
- **X-User-Id header**: The api-gateway injects `X-User-Id` from the JWT on every proxied request. Services must read user identity from this header — they never handle auth themselves.
- **EF Core migrations**: Run `db.Database.Migrate()` on startup. Add new migrations locally with `dotnet ef migrations add <Name> --project services/<svc>/<svc>.csproj`.
- **Nullable enabled**: All C# projects have `<Nullable>enable</Nullable>`. No pragma suppression.
- **ArgumentNullException.ThrowIfNull()** on all non-nullable method arguments.
- **Serilog → Seq**: All structured logging goes to Seq (`http://seq:80` inside Docker).
- **Event contracts** live in `shared/contracts/EventContracts.csproj`. Do not duplicate them in individual services.
- **Tests in `Tests/` subdirectory** of each service. The `<Compile Remove="Tests\**" />` exclusion is already in each `.csproj` — keep it.

---

## Shared Prerequisites (do before spawning agents)

1. Ensure MinIO bucket `receipts` is auto-created on startup (receipt-service must call `MakeBucketAsync` if it doesn't exist).
2. Add `AWSSDK.S3` or `Minio` NuGet package to receipt-service for MinIO access.

---

## Agent Assignments

Agents A–E can run **fully in parallel**. Agent F (frontend) can start in parallel but depends on the API shape being stable — coordinate via the contract tables below. Agent G (review) runs **after all others complete**.

---

### Agent A — user-service

**Goal:** Implement real CRUD endpoints and event publishing.

**Files to create/modify:**
- `services/user-service/src/Application/UseCases/UpsertUserUseCase.cs`
- `services/user-service/src/Application/UseCases/GetUserUseCase.cs`
- `services/user-service/src/Application/UseCases/UpdatePreferencesUseCase.cs`
- `services/user-service/src/Presentation/UsersController.cs` — replace stub
- `services/user-service/src/Infrastructure/Messaging/UserCreatedPublisher.cs`
- `services/user-service/Tests/` — use cases + controller tests

**Endpoints to implement:**

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | /users/upsert | X-User-Id header | Create or update user from Google profile. Called by api-gateway after OAuth. Returns `{ id, email, displayName }`. |
| GET | /users/me | X-User-Id header | Return current user profile. |
| GET | /users/{id}/preferences | X-User-Id header | Return user preferences list. |
| PUT | /users/{id}/preferences | X-User-Id header | Replace user preferences. Body: `[{ preferenceKey, value }]`. |

**Events to publish:**
- `UserCreatedEvent` (from `EventContracts.Events`) — publish via MassTransit `IPublishEndpoint` when a new user is created (not on update).

**MassTransit setup:**
```csharp
// In DI registration:
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(configuration["RabbitMQ__Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(configuration["RabbitMQ__User"] ?? "guest");
            h.Password(configuration["RabbitMQ__Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(ctx);
    });
});
```

**Test requirements (80% coverage):**
- `UpsertUserUseCase`: new user → publishes event + returns user; existing user → no event published + returns updated user
- `GetUserUseCase`: found → returns dto; not found → throws `NotFoundException`
- `UpdatePreferencesUseCase`: valid input → replaces preferences; invalid user → throws

---

### Agent B — receipt-service

**Goal:** Implement receipt upload, MinIO storage, OCR stub, and event publishing.

**Files to create/modify:**
- `services/receipt-service/src/Application/UseCases/UploadReceiptUseCase.cs`
- `services/receipt-service/src/Application/UseCases/GetReceiptsUseCase.cs`
- `services/receipt-service/src/Application/UseCases/GetReceiptUseCase.cs`
- `services/receipt-service/src/Infrastructure/Storage/MinioReceiptStorage.cs`
- `services/receipt-service/src/Infrastructure/Ocr/TesseractOcrService.cs` — stub: always returns 2–3 fake items
- `services/receipt-service/src/Infrastructure/Ocr/IOcrService.cs`
- `services/receipt-service/src/Presentation/ReceiptsController.cs` — replace stub
- `services/receipt-service/Tests/`

**Add to receipt-service.csproj:**
```xml
<PackageReference Include="Minio" Version="6.0.3" />
```

**Endpoints to implement:**

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | /receipts | X-User-Id | Multipart form: `file` (image). Stores to MinIO, runs OCR, persists receipt + items. Returns receipt DTO. |
| GET | /receipts | X-User-Id | List receipts for current user, ordered by CreatedAt desc. |
| GET | /receipts/{id} | X-User-Id | Single receipt with items. 404 if not owned by user. |

**Receipt status flow:** `Pending` → `Processing` → `Processed`

**OCR stub (TesseractOcrService):** Return a hardcoded list of 3 items:
```csharp
return new List<ExtractedItem>
{
    new("Milk 1L", 1, 1.29m),
    new("Bread", 2, 0.89m),
    new("Orange Juice", 1, 2.49m),
};
```

**Events to publish (in order):**
1. `ReceiptCreatedEvent` — immediately after persisting the receipt
2. `ItemsExtractedEvent` — after OCR completes and items are saved. Payload includes `List<ExtractedItem>`.

**MinIO:** Use `http://minio:9000` (from env `MinIO__Endpoint`). Bucket name from env `MinIO__BucketName` (default: `receipts`). Create bucket on startup if missing.

**Test requirements:**
- `UploadReceiptUseCase`: happy path (mock storage + OCR + publisher); OCR failure → receipt stays in `Processing` status, no ItemsExtractedEvent
- `GetReceiptsUseCase`: returns only receipts for requesting user
- `GetReceiptUseCase`: 404 for wrong user

---

### Agent C — deal-service

**Goal:** Implement deal CRUD and event publishing.

**Files to create/modify:**
- `services/deal-service/src/Application/UseCases/CreateDealUseCase.cs`
- `services/deal-service/src/Application/UseCases/ListDealsUseCase.cs`
- `services/deal-service/src/Application/UseCases/UpdateDealUseCase.cs`
- `services/deal-service/src/Application/UseCases/ArchiveDealUseCase.cs`
- `services/deal-service/src/Presentation/DealsController.cs` — replace stub
- `services/deal-service/Tests/`

**Endpoints:**

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | /deals | X-User-Id | Create a deal. Body: `{ title, description, discountAmount, locationZip? }`. |
| GET | /deals | X-User-Id | List active deals. Optional query: `?zip=<zip>` filters by locationZip. |
| GET | /deals/{id} | X-User-Id | Single deal. |
| PUT | /deals/{id} | X-User-Id | Update deal. |
| DELETE | /deals/{id} | X-User-Id | Archive deal (soft delete: `IsActive = false`). |

**Events to publish:**
- `DealCreatedEvent` on POST
- `DealUpdatedEvent` on PUT
- `DealArchivedEvent` on DELETE

**Test requirements:**
- `CreateDealUseCase`: valid input → deal persisted + event published
- `ListDealsUseCase`: active-only filter works; zip filter works
- `ArchiveDealUseCase`: sets `IsActive = false`, publishes event

---

### Agent D — matching-service

**Goal:** Consume `ItemsExtractedEvent`, run matching logic against deals, persist matches, publish savings events.

**Files to create/modify:**
- `services/matching-service/src/Application/Consumers/ItemsExtractedConsumer.cs`
- `services/matching-service/src/Application/UseCases/MatchItemsUseCase.cs`
- `services/matching-service/src/Application/UseCases/GetMatchesUseCase.cs`
- `services/matching-service/src/Infrastructure/DealServiceClient.cs` — HTTP client to deal-service
- `services/matching-service/src/Presentation/MatchesController.cs` — replace stub
- `services/matching-service/Tests/`

**Matching logic (stub — always-no-match unless deal title contains item description keyword):**
```csharp
// Simple keyword match: deal.Title contains any word from item.Description (case-insensitive)
var matched = deals.Where(d =>
    items.Any(i => d.Title.Contains(i.Description, StringComparison.OrdinalIgnoreCase)));
```

**Consumer (MassTransit IConsumer<ItemsExtractedEvent>):**
1. Fetch active deals from deal-service via HTTP (`GET /deals`)
2. Run matching logic
3. Persist `PurchaseDealMatch` records
4. If any matches found: publish `PotentialSavingsFoundEvent`
5. If no matches: still store a `RecommendationCache` record (empty) for the receipt

**Endpoints:**

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /matches | X-User-Id | List all matches for current user, ordered by CreatedAt desc. |
| GET | /matches/{receiptId} | X-User-Id | Matches for a specific receipt. |

**HTTP client to deal-service:**
```csharp
services.AddHttpClient("deal-service", c =>
    c.BaseAddress = new Uri(configuration["Services:DealService"] ?? "http://deal-service:8083"));
```
Note: pass `X-User-Id` header through when calling deal-service internally.

**Test requirements:**
- `MatchItemsUseCase`: items with keyword match → returns matched deals; no keyword match → returns empty
- `ItemsExtractedConsumer`: match found → persists match + publishes event; no match → no event published
- `GetMatchesUseCase`: returns only matches for requesting user

---

### Agent E — notification-service

**Goal:** Consume `PotentialSavingsFoundEvent`, persist in-app notifications, expose read/unread API.

**Files to create/modify:**
- `services/notification-service/src/Application/Consumers/PotentialSavingsFoundConsumer.cs`
- `services/notification-service/src/Application/UseCases/GetNotificationsUseCase.cs`
- `services/notification-service/src/Application/UseCases/MarkNotificationReadUseCase.cs`
- `services/notification-service/src/Presentation/NotificationsController.cs` — replace stub
- `services/notification-service/Tests/`

**Domain update** — `NotificationLog` should have:
```
Id, UserId, ReceiptId, Message, IsRead, CreatedAt
```

**Consumer (MassTransit IConsumer<PotentialSavingsFoundEvent>):**
1. Create a `NotificationLog` record per user with message:
   `"We found {matchCount} deal(s) matching your receipt from {storeName}!"`
2. Persist to DB

**Endpoints:**

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /notifications | X-User-Id | List notifications for user, unread first, then by CreatedAt desc. |
| PATCH | /notifications/{id}/read | X-User-Id | Mark a notification as read. 404 if not owned by user. |

**Test requirements:**
- `PotentialSavingsFoundConsumer`: creates `NotificationLog` with correct message
- `GetNotificationsUseCase`: returns only notifications for requesting user, correct ordering
- `MarkNotificationReadUseCase`: sets `IsRead = true`; 404 on wrong user

---

### Agent F — Frontend (React)

**Goal:** Implement the user-facing UI for the full vertical slice.

**Working directory:** `frontend/react-app/src/`

**Pages/components to build:**

#### 1. Dashboard (`/dashboard`)
- Protected route (redirect to `/` if no JWT)
- Shows: greeting with user's name, list of recent receipts, unread notification count badge

#### 2. Receipt Upload (`/receipts/upload`)
- Form with file input (accept `image/*`)
- On submit: `POST /api/receipts` with `multipart/form-data`
- Show upload progress → redirect to receipt detail on success

#### 3. Receipt List (`/receipts`)
- Table/card list from `GET /api/receipts`
- Each row: store name, date, total amount, status badge, link to detail

#### 4. Receipt Detail (`/receipts/:id`)
- Shows receipt info + extracted items table
- Shows matching deals section (`GET /api/matches/:id`)
- "No matches found" state if empty

#### 5. Notifications (`/notifications`)
- List from `GET /api/notifications`
- Unread items highlighted
- Click → calls `PATCH /api/notifications/:id/read`

#### 6. Navigation
- Top nav bar: Dashboard | Receipts | Notifications (with unread count badge) | Logout

**Axios base config** (`src/api/client.ts`):
```typescript
import axios from 'axios';
const client = axios.create({ baseURL: '/api' });
client.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});
export default client;
```

**Routing:** Use React Router v6. Protected routes redirect to `/` (login) when no token.

**Styling:** Keep it minimal — plain CSS or inline styles. No heavy UI library needed for MVP.

**No new npm packages** beyond what's already in `package.json` unless essential (React Router and Axios are already included).

---

### Agent G — Senior Dev Review

Run **after all other agents complete**. See `.claude/agents/senior-dev-reviewer.md` for the full agent definition and checklist.

Spawn with prompt:
```
Review all code written in Wave 2 of the expense tracker project.
The repo is at /Users/jonathantrefz/sources/redesigned-potato.
Follow the instructions in .claude/agents/senior-dev-reviewer.md exactly.
```

---

## Event Contract Reference

All events are in `shared/contracts/EventContracts.csproj`, namespace `EventContracts.Events`.

| Event | Publisher | Consumer | Key Fields |
|-------|-----------|----------|------------|
| `UserCreatedEvent` | user-service | (future) | UserId, Email, DisplayName |
| `ReceiptCreatedEvent` | receipt-service | (future) | ReceiptId, UserId, StoreName |
| `ItemsExtractedEvent` | receipt-service | matching-service | ReceiptId, UserId, StoreName, Items: List<ExtractedItem> |
| `DealCreatedEvent` | deal-service | (future) | DealId, Title, DiscountAmount |
| `DealUpdatedEvent` | deal-service | (future) | DealId, Title, DiscountAmount |
| `DealArchivedEvent` | deal-service | (future) | DealId |
| `PotentialSavingsFoundEvent` | matching-service | notification-service | ReceiptId, UserId, StoreName, MatchCount, TotalSavings |

If an event is missing a field needed for your agent's work, **add the field to the shared contract** — do not create a local copy.

---

## Environment Variables Reference

Already in `docker-compose.yml` and `.env.example`. Services read config via `builder.Configuration[...]`.

| Variable | Used by | Default |
|----------|---------|---------|
| `Services__DealService` | matching-service | `http://deal-service:8083` |
| `Services__UserService` | api-gateway | `http://user-service:8081` |
| `MinIO__Endpoint` | receipt-service | `http://minio:9000` |
| `MinIO__BucketName` | receipt-service | `receipts` |
| `MinIO__AccessKey` | receipt-service | value of `MINIO_ROOT_USER` |
| `MinIO__SecretKey` | receipt-service | value of `MINIO_ROOT_PASSWORD` |
| `RabbitMQ__Host` | all services | `rabbitmq` |
| `RabbitMQ__User` | all services | `guest` |
| `RabbitMQ__Password` | all services | `guest` |

Add any new env vars to both `docker-compose.yml` and `.env.example`.

---

## Definition of Done

An agent's work is complete when:

- [ ] All new endpoints return correct HTTP status codes (200/201/404/400)
- [ ] All health checks still return 200 (`dotnet build` passes locally or in CI)
- [ ] Unit tests pass: `dotnet test services/<name>/Tests/<name>.Tests.csproj`
- [ ] No compiler warnings related to nullable reference types
- [ ] No `#pragma warning disable` suppressions
- [ ] `ArgumentNullException.ThrowIfNull()` used on all non-nullable parameters
- [ ] Serilog structured logging on all significant operations (not just errors)
- [ ] New migrations added if domain model changed (`dotnet ef migrations add`)
- [ ] `docker-compose.yml` / `.env.example` updated if new env vars introduced
- [ ] Changes committed to a branch `wave2/<agent-name>` and pushed
