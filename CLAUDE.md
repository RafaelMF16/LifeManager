# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

LifeManager is a personal finance / life management API (.NET 10, C#, PostgreSQL via EF Core + Npgsql). It is consumed by the sibling React SPA `LifeManagerFront` (`../LifeManagerFront`, served at `https://localhost:5173`).

Current state:
- **Exposed over HTTP:** Auth (register/login/logout), UserPreferences (get/save theme + language), and Categories (full CRUD with a paged, searchable listing).
- **Domain only (no persistence, service or controller yet):** `Transactions` and `MonthlySummaries`.

## Commands

Build and test from the repo root (`LifeManager.slnx` is the solution file):

```
dotnet build LifeManager.slnx
dotnet test LifeManager.slnx
```

Run a single test project:

```
dotnet test LifeManager.Domain.Test
dotnet test LifeManager.Application.Test
```

Run a single test (by fully qualified name or filter):

```
dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~UserTests.Create_ShouldReturnUser_WhenUserIsValid"
```

Run the API:

```
dotnet run --project LifeManager.WebApi
```

Migrations (EF Core tools, `dotnet ef`): migrations live in `LifeManager.Infrastructure/Migrations`, and the startup project is the WebApi:

```
dotnet ef migrations add <Name> --project LifeManager.Infrastructure --startup-project LifeManager.WebApi
dotnet ef database update --project LifeManager.Infrastructure --startup-project LifeManager.WebApi
```

Required configuration keys, read from `IConfiguration` (user secrets or environment variables); startup throws if any is missing:
- `lifeManagerConnectionString`: the Postgres connection.
- `accessTokenSecretKey` and `refreshTokenSecretKey`: the JWT signing secrets.

Note: `LifeManager.Tests` (singular) is a leftover scaffold project. It is not referenced in `LifeManager.slnx` and contains no code beyond its `.csproj`. The real test suites are `LifeManager.Domain.Test` and `LifeManager.Application.Test`.

## Architecture

Layered/Clean Architecture split across four projects, referencing inward only. Every layer uses the same feature folders (`Auth`, `Users`, `UsersPreferences`, `Categories`, ...), plus a `Shared/` folder for cross-cutting building blocks.

- **LifeManager.Domain**
  - Holds entities, value objects, domain errors and repository interfaces, with no external dependencies.
  - Each feature folder has `ValueObjects/`, `Errors/` and (where relevant) `Interfaces/`.
  - `Shared/` holds `Results/` (Result pattern), `Paging/` (`PageRequest`, `PagedList<T>`, `PagingErrors`), `Text/` (`SearchText`) and `Enums/` (`MoneyFlowType`, `SortDirection`).
  - `InternalsVisibleTo` exposes internals to Infrastructure, so value objects' `internal static FromPersistence(...)` rehydrate from the database without re-validating.
- **LifeManager.Application**
  - Application services orchestrate domain logic: `AuthService`, `TokenService`, `UserService`, `UserPreferencesService`, `CategoryService`, `EnvironmentVariableService`.
  - Each feature has its DTOs; `Shared/DTOs/` holds `PagedResponseDto<T>`.
  - Depends on `LifeManager.Domain` only.
  - Services are registered as scoped in `DI/DependencyInjection.cs` (`AddApplicationServices`).
- **LifeManager.Infrastructure**
  - Persistence with EF Core + Npgsql. `Postgres/LifeManagerDbContext.cs` has the DbSets `Users`, `RefreshTokens`, `UserPreferences`, `Categories` and declares the `pg_trgm` extension.
  - One `IEntityTypeConfiguration<T>` per entity lives in `Postgres/Configurations/` and is applied via `ApplyConfigurationsFromAssembly`. Value objects are mapped with `HasConversion(vo => vo.Value, v => X.FromPersistence(v))`.
  - Naming is EF's default PascalCase (tables `"Categories"`, columns `"UserId"`), so any raw SQL must quote identifiers.
  - Repositories live in feature folders (`Users/`, `Auth/`, `UsersPreferences/`, `Categories/`). Shared query helpers live in `Postgres/Extensions/`.
  - Registered in `DI/DependencyInjection.cs` (`AddInfrastructureServices(connectionString)`).
- **LifeManager.WebApi**
  - ASP.NET Core host. Controllers live in feature folders (`Auth/Controllers`, `UsersPreferences/Controllers`, `Categories/Controllers`) under `[Route("api/[controller]")]`.
  - `Program.cs` wires controllers (enums serialized as strings via `JsonStringEnumConverter`), OpenAPI (Development only), Infrastructure, Application, and `DI/DependencyInjection.cs` (`AddApiServices`: JWT bearer auth + the `AllowFrontend` CORS policy for `https://localhost:5173` with credentials).
  - Middleware order: `ExceptionHandlingMiddleware` → CORS → HTTPS redirection → authentication → authorization.

Test projects mirror the layer they test 1:1 (`LifeManager.Domain.Test` → Domain, `LifeManager.Application.Test` → Application) and reference only that layer (plus Domain, transitively). **There are no Infrastructure/WebApi tests and nothing runs against a real Postgres**, so repository queries, EF mappings and migrations are only verified by running the API.

### HTTP layer conventions

- **Result → HTTP status:** services return `Result`/`Result<T>`, and controllers turn them into responses with `.Match(...)` (`WebApi/Extensions/ResultExtensions.cs`).
  - `Validation` → 400, `Conflict` → 409, `NotFound` → 404, `Unauthorized` → 401, anything else → 500.
  - The body is the `Error` (`{ code, message, type }`), which the frontend maps by `code`.
- **Current user:** authenticated endpoints are `[Authorize]` and read it with `User.GetUserId()` (`WebApi/Extensions/ClaimsExtensions.cs`, from the `NameIdentifier` claim). Every query is scoped by that `UserId`; the user is never taken from the request body or route.
- **`ExceptionHandlingMiddleware`:**
  - Turns unhandled exceptions into 500 (or 400 for `BadHttpRequestException`) with an `Error` body.
  - Swallows `OperationCanceledException` when `RequestAborted` fired and only logs it at Information level. The frontend aborts GET requests (`AbortController`) when it no longer needs them, so these logs are expected.

### Auth

- `POST /api/Auth/Register` → 201.
- `POST /api/Auth/Login`:
  - Returns `{ accessToken }`.
  - Sets the refresh token as an `HttpOnly`, `Secure` cookie `refreshToken` (path `/api/Auth`, 7 days).
- `POST /api/Auth/Logout` → 204:
  - **Anonymous on purpose** (no `[Authorize]`): the refresh token cookie is the credential, so logout still works after the access token expired. `SameSite=Lax` keeps cross-site POSTs from sending the cookie.
  - **Idempotent:** always deletes the cookie and returns 204, even with no cookie or an unknown/already revoked token (it never reveals whether a token exists). Only a missing `refreshTokenSecretKey` turns into 500.
  - Revokes by hash (`TokenService.RevokeRefreshTokenAsync` → `IRefreshTokenRepository.RevokeByHashAsync`, a single `ExecuteUpdateAsync` backed by the unique index on `TokenHash`). This is the first async piece of Auth.
  - The access token is a stateless JWT, so it stays valid until it expires (≤ 15 min). There is no JWT denylist.
- The cookie's name/path/flags live only in `WebApi/Auth/RefreshTokenCookie.cs` (`Append`/`Delete`/`Read`). `Delete` must use the same `Path`/`Secure`/`SameSite` as `Append`, or the browser keeps the cookie.
- `TokenService`:
  - Reads the secrets via `EnvironmentVariableService` (backed by `IConfiguration`).
  - Access tokens expire in **15 minutes**, and JWT validation uses `ClockSkew = TimeSpan.Zero`. Refresh tokens last 7 days.
  - Refresh tokens are stored hashed (HMAC-SHA256), and any previously active token for a user is revoked when a new one is issued.
- **There is no refresh endpoint yet.** The cookie is set but nothing consumes it, so clients get 401 once the access token expires and must log in again.

### Domain-Driven Design

`LifeManager.Domain` is modeled with DDD tactical patterns, and the folder layout is the ubiquitous language:

- **Feature folders as bounded contexts:** `Users`, `Auth`, `UsersPreferences`, `Categories`, `Transactions` and `MonthlySummaries` each own their entity, value objects, errors and repository interface. Cross-context references mostly go through IDs (e.g. `Category.UserId`, `Transaction.MonthlySummaryId`). The exception is `Transaction.Category`, which is currently an object reference.
- **Entities** (`User`, `RefreshToken`, `UserPreferences`, `Category`, `Transaction`, `MonthlySummary`) have identity (`Id`) and encapsulate their own invariants.
  - Private constructors force construction through a validating `static Create(...)` factory.
  - Mutation happens only through intention-revealing methods (`AssignId`, `RevokeToken`, `Rename`) rather than public setters.
- **Value objects** (`Email`, `UserName`, `PasswordHash`, `PlainPassword`, `CategoryName`, `TransactionAmount`, `RefreshTokenHash`, id types like `UserId`/`CategoryId`, etc.) are immutable, validate themselves in `Create`, and implement structural `Equals`/`GetHashCode`. They are the primitives that make illegal states unrepresentable instead of passing raw strings/decimals around.
- **Domain errors as part of the model:** each context's `*Errors` static class (`UserErrors`, `AuthErrors`, `CategoryErrors`, ...) enumerates the domain's known failure modes by name (e.g. `UserErrors.EmailRegistered`, `CategoryErrors.NameAlreadyExists`). Failure cases are discoverable and testable like any other part of the ubiquitous language, not ad-hoc exception messages. Error codes (`"Category.NameAlreadyExists"`) are part of the API contract: the frontend maps them to form fields and translated messages, so don't rename them casually.
- **Repository interfaces** live in the Domain layer, expressed in domain terms, and are implemented in `LifeManager.Infrastructure` (a standard DDD/hexagonal port-adapter split).

### Result pattern (no exceptions for expected failures)

The codebase is mid-migration to a `Result`/`Result<T>` pattern (`LifeManager.Domain/Shared/Results/`) for anything that can fail validation, mirroring a typical Railway-Oriented-Programming style:

- `Error` is a record with a `Code`, `Message`, and `ErrorType` (`Validation`, `NotFound`, `Unauthorized`, `Failure`, `Conflict`), created via static factories (`Error.Validation(...)`, `Error.Conflict(...)`, etc.).
- `Result` / `Result<T>` have implicit conversions from `Error` and from `T`, so factory methods can `return SomeErrors.Whatever;` or `return new Thing(...)` directly instead of throwing.
- `ResultExtensions` provides `Map`, `Bind`, and `Tap` for chaining `Result<T>` operations functionally (see `User.Create` and `TokenService.GenerateTokens`/`SaveRefreshToken` for the chaining style).
- `Users`, `Auth`, `UsersPreferences` and `Categories` have been migrated to this pattern.
- `Transactions` and `MonthlySummaries` have **not** been migrated yet. Their `Create` methods return the entity/value object directly and use `DomainException` for invariant violations (see `Transaction.Create` throwing `DomainException` when the money-flow type mismatches). When touching these areas, check with the user whether to migrate them to `Result` first, since this is an active, incremental refactor.

### Categories (reference flow for new features)

- **Name rules:** `Category` has no `MoneyFlowType`. The type lives only on `Transaction`, so the same category can be used for expenses and income.
  - Only the name can change (`Rename`).
  - Names are unique per user **ignoring case and accents**: `Category.NormalizedName` (from `CategoryName.NormalizedValue`) backs the unique index `(UserId, NormalizedName)`. So "Mercado", "mercado" and "Mercadó" conflict (`Category.NameAlreadyExists`), but renaming to a different casing of the same name is allowed.
- **Async flow:** repository → service → controller is **async with `CancellationToken`**.
- **Repository EF conventions** to reuse in new flows:
  - `AsNoTracking` on reads.
  - Filtering by `UserId` in the database.
  - `AnyAsync` for existence checks.
  - `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for single-round-trip writes. `ExecuteUpdateAsync` bypasses the entity, so it must set **every** derived column too, e.g. both `Name` and `NormalizedName`.
- **Older flows:** User, Auth and UsersPreferences are still synchronous (except Auth's logout, which is async).

### Paged listings (standard for every list endpoint)

Every list endpoint is paginated, searched and sorted **in the database**; never return a whole collection for the client to slice. `GET /api/Categories` is the reference implementation:

- **Contract:**
  - Query parameters come from a `[FromQuery]` DTO with `Page = 1`, `PageSize = PageRequest.DefaultPageSize` (20), `Search?` and `SortDirection = Asc` (`Asc|Desc`, bound by name).
  - The response is `PagedResponseDto<T>(Items, TotalCount, Page, PageSize, TotalPages)` (`LifeManager.Application/Shared/DTOs`), built with `PagedResponseDto<T>.From(pagedList, map)`.
  - The service returns `Result<PagedResponseDto<T>>`, and the controller uses `.Match(Ok)`.
- **Validation:** `PageRequest.Create(page, pageSize)` (`LifeManager.Domain/Shared/Paging`) returns `PagingErrors.InvalidPage` (page < 1) or `PagingErrors.InvalidPageSize` (outside 1..`MaxPageSize` = 100), which become 400.
- **Offset pagination:**
  - The repository filters by `UserId`, applies search, and orders **deterministically (always end with `Id` as tiebreaker)**.
  - It then calls `ToPagedListAsync(pageRequest, ct)` (`LifeManager.Infrastructure/Postgres/Extensions/QueryablePagingExtensions.cs`), which runs `COUNT` + `Skip`/`Take` and returns a `PagedList<T>`.
  - Offset was chosen over keyset because the UI has numbered pages and per-user volumes are small: every query hits only one user's slice of the index.
- **Search (accent/case-insensitive, indexed):**
  - The Domain normalizes with `SearchText.Normalize` (`LifeManager.Domain/Shared/Text`: trim + lowercase + strip accents). The searchable text is persisted in its own column (e.g. `Category.NormalizedName`, kept in sync in `Create`/`Rename`, and also set in `ExecuteUpdateAsync`). Never normalize in SQL at query time.
  - That column gets a GIN trigram index: `HasIndex(x => x.NormalizedX).HasMethod("gin").HasOperators("gin_trgm_ops")`. `pg_trgm` is declared via `HasPostgresExtension` in `LifeManagerDbContext`.
  - Query with `EF.Functions.Like(x.NormalizedX, QueryablePagingExtensions.ToContainsLikePattern(term), QueryablePagingExtensions.LikeEscapeCharacter)`. The helper escapes `%`, `_` and `\`. **Do not use `string.Contains`**: Npgsql translates it to `strpos`, which can't use the trigram index.
  - The service normalizes the incoming term with the same `SearchText.Normalize`; an empty result means "no filter".
- **Migrations adding a normalized column** must backfill existing rows before creating unique indexes. See `AddCategoryNormalizedNameAndSearchIndex`: it uses `lower(unaccent(btrim(...)))`, with the `unaccent` extension needed only for that one-off `UPDATE`.
- **Tests:** the in-memory repository mock must mirror the real query (see `CategoryRepositoryMock.GetPagedByUserIdAsync`):
  - filter with `NormalizedX.Contains(term, Ordinal)`;
  - use the same ordering, with the `Id` tiebreaker;
  - apply `Skip`/`Take` and return the total count.

  Cover at least: search without accents/case, second page, `Desc`, page beyond the last, and invalid `page`/`pageSize`.

### Application services and DI

Services use primary-constructor dependency injection (e.g. `CategoryService(ICategoryRepository categoryRepository)`, `TokenService(IRefreshTokenRepository refreshTokenRepository, EnvironmentVariableService environmentVariableService)`) against Domain repository interfaces. A new feature needs to be registered in three places:
- its service in `LifeManager.Application/DI/DependencyInjection.cs`;
- its repository in `LifeManager.Infrastructure/DI/DependencyInjection.cs`;
- its mock in `LifeManager.Application.Test/Configurations/InjectionModule.cs`.

## Testing conventions

- xUnit, `Assert.*` style, with hand-written mocks (no Moq/NSubstitute). Test names follow `Method_ShouldExpectedBehavior_WhenCondition`.
- **`LifeManager.Application.Test`:**
  - **Container:** each test class builds its own mini DI container via `BaseTest` (in `Configurations/`). It registers the real application services against in-memory repository mocks (`*/Mocks`, wired in `Configurations/InjectionModule.cs`) and an in-memory `IConfiguration` with test secret keys.
  - **Resolving services:** test classes extend `BaseTest` and call `ServiceProvider.GetRequiredService<T>()`. To assert on call counters, cast the repository back to its mock, e.g. `(CategoryRepositoryMock)ServiceProvider.GetRequiredService<ICategoryRepository>()`.
  - **Shared data:** the mocks store data in shared singleton in-memory lists (`Configurations/SingletonLists/*Singleton.cs`), so test classes are tagged `[Collection("ApplicationServices")]` to run serially. Clear the relevant singleton in the test class constructor (e.g. `CategorySingleton.Instance.Clear()`).
  - **Mocks must behave like the real repository:** same filtering, ordering, uniqueness and paging rules. Otherwise service tests pass against behavior production doesn't have.
- `LifeManager.Domain.Test` tests value objects and entities directly with no DI, asserting on `Result.IsSuccess`/`Result.Error` and value-object `.Value` properties. Shared building blocks (`SearchText`, `PageRequest`/`PagedList`) are tested in `Shared/`.
