# Wave 2 Code Review

**Reviewer:** Senior Dev Agent
**Date:** 2026-05-19
**Scope:** Wave 2 implementation (services: user, receipt, deal, matching, notification + frontend)

---

## Executive Summary

The Wave 2 scaffold delivers a coherent microservices skeleton with clean separation of concerns in most services, solid test coverage in user-service and deal-service, and correct secrets hygiene in committed files. However, there are three issues that must be fixed before this is safe to merge to main: a real Google OAuth client secret and JWT signing key are committed to `.env` (which is in `.gitignore` but present on disk and could be accidentally staged), a critical RabbitMQ configuration key mismatch will cause notification-service, user-service, and deal-service to silently fall back to the hardcoded `guest/guest` credentials in any environment where the compose env vars are used, and the receipt-service controller commits a clean architecture violation by embedding all business logic directly in the controller. Several major issues around missing file-upload validation, unbounded list queries, and the `continue-on-error: true` CI flag also need prompt attention.

---

## Critical Issues (must fix before merge)

1. **Real credentials committed to `.env`** — Google OAuth client secret and JWT signing key are in the `.env` file on disk with real values. While `.env` is listed in `.gitignore`, the file exists and a careless `git add .` will expose it. Rotate `GOOGLE_CLIENT_SECRET` and `JWT_SECRET` immediately; confirm `.env` is not tracked with `git ls-files .env`.

2. **RabbitMQ config key mismatch — consumers silently use `guest/guest`** — notification-service, user-service, and deal-service read `RabbitMQ__Host`, `RabbitMQ__User`, `RabbitMQ__Password` (double-underscore, `User` not `Username`), but `docker-compose.yml` sets `RabbitMq__Host`, `RabbitMq__Username`, `RabbitMq__Password` (single-separator, `Username`). The keys never match, so those three services fall back to the hardcoded default `"guest"` strings on every environment. Services will connect, but with wrong credentials once RabbitMQ is hardened.

3. **Receipt controller is a clean architecture violation** — `services/receipt-service/src/Controllers/ReceiptsController.cs` calls `_db.Receipts.Add()`, `_db.SaveChangesAsync()`, and `_publish.Publish()` directly in the controller action (lines 60–99). All other services route this through use-case classes. The `ReceiptDbContext` is injected into the controller constructor. Move OCR orchestration, persistence, and event publishing into a `UploadReceiptUseCase` class under `src/Application/UseCases/`.

---

## Major Issues (fix soon)

1. **No file-type or file-size validation on receipt upload** — `ReceiptsController.Upload` (line 60) accepts any `IFormFile` without checking MIME type, extension, or byte size. A caller can upload a 2 GB executable. Add: `if (!request.Image.ContentType.StartsWith("image/")) return BadRequest("…");` and enforce a max size (e.g. 10 MB) using `request.Image.Length`.

2. **MinIO storage path not scoped to user ID** — `MinioStorageService.UploadAsync` (line 25) builds the object key as `{bucketName}/{Guid}/{fileName}`. The user's ID is not part of the path. Any signed URL or internal enumeration can reveal another user's receipts. Change the key pattern to `{userId}/{Guid}/{sanitized-filename}`.

3. **JWT token passed as URL query parameter** — `AuthController.GoogleCallback` (line 49) redirects to `{frontendUrl}/auth/callback?token={token}`. JWT tokens in query strings appear in server access logs, browser history, and `Referer` headers. Change this to a short-lived, opaque code that the frontend exchanges for the token via a POST, or at a minimum use a fragment (`#token=…`) which is never sent to the server.

4. **`continue-on-error: true` on test step masks failures** — `.github/workflows/build-and-test.yml` line 49 sets `continue-on-error: true` on every `dotnet test` step. A test failure will never fail the build. Remove this flag; let tests gate the pipeline.

5. **Unbounded list queries with no pagination** — `ListDealsUseCase.ExecuteAsync` (line 34), `GetNotificationsUseCase.ExecuteAsync` (line 28), and the `UpdatePreferencesUseCase.GetForUserAsync` (line 93) all call `.ToListAsync()` with no `Take()` or cursor. Any of these can return an arbitrarily large result set. Add `[FromQuery] int pageSize = 50, int page = 0` parameters and `.Skip(page * pageSize).Take(pageSize)` to at least the deals and notifications endpoints.

6. **`user-service` and `deal-service` target `net8.0`, all others target `net10.0`** — `services/user-service/user-service.csproj` and `services/deal-service/deal-service.csproj` declare `<TargetFramework>net8.0</TargetFramework>`. The CI workflow installs `dotnet-version: "10.0.x"` and the Dockerfiles use `sdk:10.0`. These services will build correctly in CI (the SDK is backwards-compatible), but the mismatch is confusing and the runtime image (`aspnet:10.0`) is not guaranteed to include net8 fallback packages. Align all services to `net10.0`.

7. **`SavingOpportunityEvent` in shared contracts is unused** — `shared/contracts/Events/SavingOpportunityEvent.cs` is declared but no consumer or producer references it anywhere in the codebase. Either wire it in or remove it to keep the contract surface honest.

---

## Minor Issues (fix in follow-up)

1. **`OcrServiceFactory` uses service locator pattern** — `OcrServiceFactory.Create()` calls `_serviceProvider.GetRequiredService<TesseractOcrService>()` (line 14). This is a service locator anti-pattern per the DI guidelines in `CLAUDE.md`. The factory should accept `TesseractOcrService` as a constructor parameter instead.

2. **`ReceiptsController.Upload` opens the stream twice** — Lines 60 and 66 both call `request.Image.OpenReadStream()`. The first call for storage upload may consume or partially advance the stream before the OCR call. Store the stream result or copy to a `MemoryStream` before passing it to both consumers.

3. **`NotificationLog.UserId` is `string` while `UserSubscription.UserId` is `Guid`** — The type inconsistency (see `NotificationLog.cs` line 6 vs `UserSubscription.cs` line 6 and the migration at line 35 vs 21) means the two domain objects in the same service use different identity types. Standardise to `Guid`.

4. **`PotentialSavingsFoundConsumer` does not pass `CancellationToken` to `SaveChangesAsync`** — `services/notification-service/src/Application/Consumers/PotentialSavingsFoundConsumer.cs` line 46 calls `_db.SaveChangesAsync()` with no cancellation token. The `ConsumeContext` has a `CancellationToken` available via `context.CancellationToken`. Same issue in `UserCreatedConsumer` (line 37).

5. **Stub `SourcesController` and `MatchesController` have no X-User-Id check** — `SourcesController.GetAll` and `MatchesController.GetAll` return data with no authentication check at all. Even if the data is currently empty, the pattern should be consistent with other controllers before real data is returned.

6. **Missing `[ApiController]` route prefix on `AuthController`** — `AuthController` (line 9) has `[ApiController]` but no `[Route]` attribute. Routes are declared inline (`[HttpGet("/auth/google")]`). This works but is inconsistent with every other controller that uses `[Route("[controller]")]`. If the class is ever renamed, the routes silently break.

7. **`#nullable disable` in generated migration file** — `services/notification-service/Migrations/20260519000000_AddNotificationLog.cs` line 4 contains `#nullable disable`, which is auto-generated by EF. This is acceptable in migrations but the CLAUDE.md guidance says no `#pragma` directives. Document in a project-level comment or suppress rule that migrations are exempt.

8. **All Dockerfiles and compose images use floating tags** — All Dockerfiles use `sdk:10.0` and `aspnet:10.0` (minor-pinned, acceptable per reviewer checklist), but `docker-compose.yml` uses `datalust/seq:latest`, `rabbitmq:3-management`, `minio/minio:latest`, `postgres:16` (no patch), and `nginx:alpine` (no version at all). Pin at least to `rabbitmq:3.13-management`, `minio/minio:RELEASE.2024-xx`, etc.

9. **`api-gateway` has no health check in `docker-compose.yml`** — Every infrastructure service and every backend service uses `condition: service_healthy` for its dependencies, but `api-gateway` itself has no `healthcheck:` stanza. The `frontend` service `depends_on: api-gateway` without a condition, so it may start before the gateway is actually ready.

10. **`FRONTEND_URL` is hardcoded to `http://localhost:3000` in `docker-compose.yml`** — Line 168 sets `FRONTEND_URL: http://localhost:3000` directly rather than using `${FRONTEND_URL:-http://localhost:3000}`. This makes it impossible to override without editing the compose file, which is an obstacle for staging/production environments.

---

## Suggestions (optional improvements)

1. **Add a global exception handler / Problem Details middleware** — None of the services register `app.UseExceptionHandler` or `builder.Services.AddProblemDetails()`. Unhandled exceptions from use cases currently bubble up as unformatted 500 responses. A global handler would give consistent RFC 7807 error shapes.

2. **Add MassTransit retry policy** — No service configures `UseMessageRetry` on any consumer endpoint. Transient RabbitMQ connection issues will cause consumers to fail without retry. Add at least `e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)))` in `ConfigureEndpoints`.

3. **`TesseractOcrService` is a named stub — make that explicit** — `TesseractOcrService.ExtractAsync` returns a hardcoded empty result. The class name implies real Tesseract integration. Rename it to `StubOcrService` or add a `// STUB: replace with real Tesseract integration` comment so the gap is not mistaken for a completed feature.

4. **JWT token stored in `localStorage` is XSS-vulnerable** — `frontend/react-app/src/auth/AuthContext.tsx` stores the JWT in `localStorage`. Any XSS vulnerability would allow a script to exfiltrate the token. Consider `HttpOnly` cookies managed by the api-gateway for production hardening.

5. **`UpsertUserUseCase` calls `SaveChangesAsync` before publishing the event** — If the event publish fails after save, the user exists in the DB but downstream services never receive `UserCreatedEvent`. For production reliability, consider the Outbox pattern (MassTransit supports this natively with EF).

6. **No `format` check in frontend CI** — `.github/workflows/frontend-ci.yml` runs `npm run lint` and `npm run build` but not `npm run format -- --check`. A Prettier drift will not fail CI.

---

## Section 1 — Architecture & Design

**Clean Architecture**

The user-service, deal-service, and notification-service follow clean architecture well: controllers delegate to use-case classes, use cases call `DbContext` directly (acceptable given no repository interface layer is targeted for this MVP), and DTOs are returned from controllers. The main violation is in receipt-service: `ReceiptsController` (lines 44–102) contains full business logic — storage upload, OCR orchestration, entity construction, DB persistence, and two event publishes — that belongs in a use case. This is the only service where the controller is not a thin routing layer.

**Service boundary**

Each service has its own PostgreSQL database with no cross-DB references. All inter-service communication is event-driven via MassTransit/RabbitMQ, which is correct. The single synchronous HTTP call (`AuthController.EnsureUserExistsAsync` calling user-service at line 54) is appropriate: the gateway must resolve a user ID before issuing a JWT, and a synchronous call here is the right model. The fallback on line 62 — returning a random Guid when the user-service call fails — is dangerous: a JWT with a fabricated user ID will be accepted by all services. At minimum, fail with a 503 rather than silently inventing an ID.

**Event contract integrity**

All events live in `shared/contracts/Events/`. No local copies were found. `SavingOpportunityEvent` is declared but unused anywhere. The `PotentialSavingsFoundEvent` lacks an `OccurredAt` timestamp (all other events have one), which will complicate debugging and ordering.

**Dependency injection**

All use cases and services are registered in DI. No `new` for services. No static state. `OcrServiceFactory` is the one service-locator pattern (see Minor Issues #1).

**MassTransit**

`ConfigureEndpoints(ctx)` is called in all six services. All consumers are registered. No retry policy is configured anywhere (see Suggestions #2). The `deal-service` and `user-service` register `MassTransit` but declare no consumers — this is correct (they are publisher-only) but `ConfigureEndpoints(ctx)` will still create receive endpoints with no consumers attached; harmless but slightly wasteful.

---

## Section 2 — Security

**JWT validation**

`ValidateIssuer = false` and `ValidateAudience = false` in both `Program.cs` (lines 55–56) and `JwtService.cs` (lines 44–45). For an internal single-issuer system this is acceptable, but it means any JWT signed with the same secret from any source will be accepted. Document the intentional decision. `ClockSkew = TimeSpan.Zero` is strict and correct. The JWT secret is never validated for minimum length; the `JwtService` constructor should assert `secret.Length >= 32`.

**Authorization enforcement**

User-service correctly checks `X-User-Id` for all protected endpoints and enforces ownership in `UpdatePreferencesUseCase`. Notification-service enforces user scoping in `MarkNotificationReadUseCase`. Receipt-service `GetById` (line 43) does NOT check `X-User-Id` — any authenticated user can retrieve any receipt by guessing a UUID. Add an ownership check: `if (receipt.UserId != requestingUserId) return NotFound();`. Deal-service endpoints are not user-scoped (deals are public), which appears intentional.

**Input validation**

No `[Required]` annotations, no FluentValidation, no `ModelState.IsValid` check in any controller. The controllers rely on nullable/non-nullable types alone. For the MVP this may be acceptable, but `CreateDealRequest.Title`, `.Description`, and `DiscountAmount` have no range or length constraints. File uploads have no validation at all (see Major Issues #1).

**Secrets management**

`appsettings.json` files contain no secrets. All sensitive values are environment-variable-driven. `.env` is in `.gitignore`. However, a real `.env` with live credentials exists on disk (see Critical Issues #1).

**OAuth security**

The `redirect_uri` override mechanism in `Program.cs` (lines 39–47) rewrites the `redirect_uri` in the outgoing auth request using user-controllable config. This is controlled by `GOOGLE_REDIRECT_URI` in the environment (not by the caller), so it is not directly exploitable, but it bypasses the ASP.NET cookie-based state parameter that CSRF-protects the OAuth flow. The `OnRedirectToAuthorizationEndpoint` event fires before the state cookie is set, which means if an attacker can observe the state parameter they can replay it. The correct fix is to let ASP.NET handle `redirect_uri` normally and configure the allowed callback URI in Google Cloud Console.

The JWT token in the redirect URL query string is a significant security issue (see Major Issues #3).

**CORS**

No `AddCors` / `UseCors` call exists in any service. In the Docker Compose setup, the nginx frontend proxies to `/api` which reaches the gateway, so browser CORS is not triggered for the frontend. However, if the API is ever called directly (e.g. from a mobile app or during development), all cross-origin requests will be blocked. Add explicit CORS policy now, restricted to `FRONTEND_URL`.

**MinIO object keys**

Object keys do not include the user ID (see Major Issues #2).

---

## Section 3 — Code Quality

**CLAUDE.md compliance**

`ArgumentNullException.ThrowIfNull()` is used consistently on all non-nullable constructor and method parameters across all services. No `#pragma warning disable` found in hand-written files (only in the EF-generated migration). Nullable reference types are enabled in all projects.

**Sync-over-async**

No `.Result` or `.Wait()` calls found. All async paths use `await`.

**Error handling**

No global exception handler is registered in any service (see Suggestions #1). `AuthController.EnsureUserExistsAsync` catches a failed HTTP call and returns a random Guid rather than propagating the error (line 62–63) — this is a silent failure that produces a structurally valid but semantically wrong JWT. The catch-all `catch` block in `JwtService.TryExtractUserId` (line 52) is intentional (any invalid token returns null) and acceptable.

**Readability**

Method lengths are appropriate throughout. No deep nesting. No magic numbers. The one naming inconsistency is `NotificationLog.UserId` (string) vs `UserSubscription.UserId` (Guid) in the same service (see Minor Issues #3). The dual-controller file pattern (an empty stub in `src/Controllers/` pointing to `src/Presentation/`) exists in user-service and deal-service. It is harmless but confusing — remove the empty stub files.

**Performance**

No N+1 queries found. No `.AsNoTracking()` on any read-only query; add it to `ListDealsUseCase`, `GetNotificationsUseCase`, and `GetUserUseCase` to avoid unnecessary change-tracking overhead. Unbounded list queries exist (see Major Issues #5). The receipt upload calls `OpenReadStream()` twice (see Minor Issues #2).

**Dead code**

`SavingOpportunityEvent` is declared but unreferenced. The empty controller stub files (`src/Controllers/UsersController.cs`, `src/Controllers/DealsController.cs`) are dead weight.

---

## Section 4 — Test Quality

**Coverage**

User-service and deal-service have excellent coverage: every use case is tested with happy path, not-found, wrong-owner, and event-not-published cases. Notification-service has thorough tests for both consumers and both use cases, including the ownership check on `MarkNotificationRead`. The weak spots are:

- **receipt-service**: Only one test (`TesseractOcrServiceTests`) which asserts the stub returns empty results. No test for `ReceiptsController.Upload`, no test for the ownership check on `GetById`, and no test for the storage or event-publish path.
- **matching-service**: One test asserting an empty controller returns 200. No tests for any of the five consumers.
- **aggregation-service**: One test asserting an empty controller returns 200.
- **api-gateway**: Two tests for `JwtService`. No test for `AuthController`, no test for the middleware that injects `X-User-Id`, and no test for the fallback Guid bug in `EnsureUserExistsAsync`.

**Test design**

Tests follow Arrange-Act-Assert consistently. Names use `Method_Scenario_Expected` or `Execute_Scenario_Expected` patterns. Each test verifies one behavior. No padding tests found.

**Mock discipline**

All tests use EF InMemory databases; no real database connections. `IPublishEndpoint`, `IDateTimeProvider`, and `ConsumeContext<T>` are all properly mocked with Moq.

**Missing test categories**

- No test for `ReceiptsController.GetById` ownership check (unauthenticated access returns the receipt).
- No test for the `AuthController` fallback Guid path (user-service 500 → random Guid in JWT).
- No test for any matching-service consumer behavior.
- No test for file-type or file-size rejection (once validation is added).

---

## Section 5 — CI/CD & DevOps

**Dockerfile quality**

All backend Dockerfiles use a clean two-stage build (`sdk` → `aspnet`). `WORKDIR` is set. Only necessary source trees are copied. The frontend Dockerfile uses `node:20-alpine` → `nginx:alpine`, which is correct. **No `.dockerignore` file exists anywhere in the repository.** Without `.dockerignore`, the `COPY shared/` and `COPY services/api-gateway/` instructions copy `bin/`, `obj/`, `Tests/`, and any `.env` files into the build context, inflating image size and potentially leaking local build artifacts. Add a root-level `.dockerignore` with at minimum `**/bin/`, `**/obj/`, `**/*.Tests/`, `.env`, `.git/`.

**Pipeline completeness**

`build-and-test.yml` builds and runs tests for all seven services. `frontend-ci.yml` runs lint and build. However:
- `continue-on-error: true` on every test step means test failures are hidden (see Major Issues #4).
- There is no step that validates Docker images actually build (`docker build` sanity check).
- `frontend-ci.yml` does not run `npm run format -- --check` (see Suggestions #6).
- No TypeScript strict check (`tsc --noEmit`) separate from the build.

**Pinned versions**

Backend base images: `sdk:10.0` / `aspnet:10.0` — minor-version pinned, acceptable.
`node:20-alpine` — major-version pinned, acceptable.
`nginx:alpine` — **unpinned**, will silently get new nginx major versions.
Compose infrastructure: `datalust/seq:latest`, `minio/minio:latest` — **unpinned floating tags**.
`postgres:16` — major-pinned only.
GitHub Actions: `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4` — major-version tags only, acceptable for internal projects.

**Secrets in CI**

No hardcoded secrets in workflow files. Variables correctly use `${{ secrets.* }}` (no secrets are referenced yet; the pipelines don't deploy anywhere).

**Health check coverage**

All six PostgreSQL databases, RabbitMQ, and MinIO have health checks. `seq` uses `condition: service_started` (Seq has no built-in health check command, so this is pragmatic). Application services (`api-gateway`, all backends) have no `healthcheck:` stanzas in the compose file. `api-gateway` does expose `/health` at the application level but no compose health check wraps it. Add health checks to all app services so that future dependent services can use `condition: service_healthy`.

---

## Verdict

**REQUEST CHANGES**

The codebase shows solid architectural intent, good test discipline in the services that matter most, and clean secrets hygiene in committed files. However, three blockers prevent safe merge: real OAuth and JWT credentials live in the on-disk `.env` file and must be rotated immediately; a RabbitMQ config key mismatch will silently fall back to hardcoded defaults and will cause production credential failures the moment RabbitMQ authentication is tightened; and `continue-on-error: true` in CI means broken tests are invisible to the team. The receipt-service clean-architecture violation and the missing file-upload validation are close seconds. Fix the three critical issues and the four flagged major issues (RabbitMQ keys, file validation, receipt architecture, CI gating) before merging to main; the remaining items can follow in a fast-follow PR.
