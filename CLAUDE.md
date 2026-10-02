# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

LifeManager is a personal finance / life management API (.NET 10, C#, PostgreSQL via EF Core + Npgsql). It is consumed by the sibling React SPA `LifeManagerFront` (`../LifeManagerFront`, served at `https://localhost:5173`).

Current state:
- **Exposed over HTTP:** Auth (register/login/refresh/logout), Users (`GET /api/Users/Me` → `{ name }` of the authenticated user, shown in the frontend's Header menu), UserPreferences (get/save theme + language), Categories (full CRUD with a paged, searchable listing), MonthlySummaries (create a month, month details, and a paged listing filtered by year/balance and sortable by period, income, expenses or balance) and Transactions (full CRUD inside a month, which keeps the month's totals up to date).

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
  - `InternalsVisibleTo` exposes internals to Infrastructure, so value objects' `internal static FromPersistence(...)` rehydrate from the database without re-validating. `LifeManager.Application.Test` also sees them, to seed stored state that the validating factories can't create.
- **LifeManager.Application**
  - Application services orchestrate domain logic: `AuthService`, `TokenService`, `UserService`, `UserPreferencesService`, `CategoryService`, `MonthlySummaryService`, `TransactionService`, `EnvironmentVariableService`.
  - Each feature has its DTOs; `Shared/DTOs/` holds `PagedResponseDto<T>`.
  - Depends on `LifeManager.Domain` only.
  - Services are registered as scoped in `DI/DependencyInjection.cs` (`AddApplicationServices`).
- **LifeManager.Infrastructure**
  - Persistence with EF Core + Npgsql. `Postgres/LifeManagerDbContext.cs` has the DbSets `Users`, `RefreshTokens`, `UserPreferences`, `Categories`, `MonthlySummaries`, `Transactions` and declares the `pg_trgm` extension.
  - One `IEntityTypeConfiguration<T>` per entity lives in `Postgres/Configurations/` and is applied via `ApplyConfigurationsFromAssembly`. Value objects are mapped with `HasConversion(vo => vo.Value, v => X.FromPersistence(v))`.
  - Naming is EF's default PascalCase (tables `"Categories"`, columns `"UserId"`), so any raw SQL must quote identifiers.
  - Repositories live in feature folders (`Users/`, `Auth/`, `UsersPreferences/`, `Categories/`, `MonthlySummaries/`, `Transactions/`). Shared query helpers live in `Postgres/Extensions/`.
  - Registered in `DI/DependencyInjection.cs` (`AddInfrastructureServices(connectionString)`).
- **LifeManager.WebApi**
  - ASP.NET Core host. Controllers live in feature folders (`Auth/Controllers`, `UsersPreferences/Controllers`, `Categories/Controllers`, `MonthlySummaries/Controllers`, `Transactions/Controllers`) under `[Route("api/[controller]")]`; `TransactionsController` is nested under its month (`api/MonthlySummaries/{monthlySummaryId}/[controller]`).
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
  - Sets the refresh token as an `HttpOnly`, `Secure` cookie `refreshToken` (path `/api/Auth`). The cookie expires together with the token (`LoginResponseDto.RefreshTokenExpiresAt`).
- `POST /api/Auth/Refresh`:
  - Returns 200 `{ accessToken }` and sets a new `refreshToken` cookie.
  - On failure it returns 401 `Auth.InvalidRefreshToken` and deletes the cookie.
  - **Anonymous on purpose**, like Logout: the cookie is the credential.
  - **Rotation:** every refresh consumes the presented token and issues a new pair (`TokenService.RefreshTokensAsync`). `IRefreshTokenRepository.TryConsumeAsync` is a conditional `ExecuteUpdateAsync` (`!IsRevoked && ExpiresAt > now`), so two concurrent refreshes with the same token can't both succeed.
  - **Reuse detection:** presenting an already revoked token is treated as theft and revokes **every** active token of that user (`RevokeAllActiveByUserIdAsync`). The frontend serializes refreshes across tabs (Web Locks) so normal use never triggers it.
  - **Sliding 7 days + absolute 30-day cap:** `SessionExpiresAt` is set at login (+30 d) and inherited by every rotation. Each new token gets `ExpiresAt = min(now + 7d, SessionExpiresAt)` (`RefreshToken.Rotate`).
  - **Same error for every failure:** missing, unknown, expired, revoked and reused tokens all return `Auth.InvalidRefreshToken`, so the API never reveals whether a token exists.
  - **No transaction:** consuming the old token and inserting the new one are separate writes (there is no Unit of Work). If the insert fails, the user simply logs in again.
- `POST /api/Auth/Logout` → 204:
  - **Anonymous on purpose** (no `[Authorize]`): the refresh token cookie is the credential, so logout still works after the access token expired. `SameSite=Lax` keeps cross-site POSTs from sending the cookie.
  - **Idempotent:** always deletes the cookie and returns 204, even with no cookie or an unknown/already revoked token (it never reveals whether a token exists). Only a missing `refreshTokenSecretKey` turns into 500.
  - Revokes by hash (`TokenService.RevokeRefreshTokenAsync` → `IRefreshTokenRepository.RevokeByHashAsync`, a single `ExecuteUpdateAsync` backed by the unique index on `TokenHash`).
  - The access token is a stateless JWT, so it stays valid until it expires (≤ 15 min). There is no JWT denylist.
- The cookie's name/path/flags live only in `WebApi/Auth/RefreshTokenCookie.cs` (`Append`/`Delete`/`Read`). `Delete` must use the same `Path`/`Secure`/`SameSite` as `Append`, or the browser keeps the cookie.
- `TokenService`:
  - Reads the secrets via `EnvironmentVariableService` (backed by `IConfiguration`).
  - Access tokens expire in **15 minutes**, and JWT validation uses `ClockSkew = TimeSpan.Zero`. Refresh tokens last 7 days (sliding), capped at 30 days per session.
  - Refresh tokens are stored hashed (HMAC-SHA256), and any previously active token for a user is revoked when a new one is issued.

### Domain-Driven Design

`LifeManager.Domain` is modeled with DDD tactical patterns, and the folder layout is the ubiquitous language:

- **Feature folders as bounded contexts:** `Users`, `Auth`, `UsersPreferences`, `Categories`, `Transactions` and `MonthlySummaries` each own their entity, value objects, errors and repository interface. Cross-context references go through IDs (e.g. `Category.UserId`, `Transaction.MonthlySummaryId`, `Transaction.CategoryId`); when a read needs data from another context (a transaction's category name), the repository joins and returns a read model (`TransactionListItem`).
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
- `ResultExtensions` provides `Map`, `Bind`, and `Tap` for chaining `Result<T>` operations functionally (see `User.Create` and `TokenService.GenerateTokensAsync` for the chaining style). The extensions are synchronous, so async steps (repository calls) go after the chain, in the early-return style of `CategoryService`.
- Every context (`Users`, `Auth`, `UsersPreferences`, `Categories`, `MonthlySummaries`, `Transactions`) uses this pattern; there is no `DomainException` anymore. New code must not throw for expected failures.

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
- **Every flow is async:** Users, Auth and UsersPreferences follow the same repository → service → controller async chain with `CancellationToken`. Keep CPU-bound work (BCrypt) synchronous, and never run queries in parallel on the same `DbContext`.

### MonthlySummaries

- **Creating a month:** `POST /api/MonthlySummaries` takes only `{ month }`; the service uses the current UTC year, since `MonthlySummaryYear.Create` only accepts the current year (`MonthlySummary.YearNotCurrent`). `FromPersistence` skips that rule, so past years rehydrate. A new month starts with zero totals. One month per user: unique index `(UserId, Year, Month)` + `MonthlySummary.AlreadyExists` (409).
- **Balance column:** `Balance` (VO) is derived and ignored by EF; `BalanceAmount` is its persisted copy (same idea as `Category.NormalizedName`) so the listing filters (`Positive` = ≥ 0, `Negative` = < 0) and sorts by it in SQL. Anything that changes the totals must also update `BalanceAmount` (including in `ExecuteUpdateAsync`).
- **Details:** `GET /api/MonthlySummaries/{id}` returns `MonthlySummaryDetailsResponseDto`: the totals plus `IncomeCount`/`ExpenseCount` (`ITransactionRepository.CountByTypeAsync`) and `PreviousId`/`NextId`, the user's closest months before and after (`IMonthlySummaryRepository.GetNeighborsAsync`, which reads only the user's month keys because Year/Month are value objects and can't be compared in SQL). Create and the listing still return `MonthlySummaryResponseDto`.
- **Totals** only change through transactions: `MonthlySummary.ApplyTotals(income, expense)` replaces both totals and keeps `BalanceAmount` in sync (see Transactions).
- **Listing:** `GET /api/MonthlySummaries?page=&pageSize=&year=&balance=All|Positive|Negative&sortBy=Period|TotalIncome|TotalExpense|Balance&sortDirection=` (default `Period`/`Desc`, newest first). Every sort ends with Year, Month, Id in the same direction. `GET /api/MonthlySummaries/Years` returns the user's distinct years (newest first) for the year filter.
- **Tests:** `MonthlySummary.FromPersistence(...)` (internal) rehydrates a stored summary without the creation rules, so tests can seed past years and non-zero totals that `Create` can't produce; the repository mock also uses it for its detached copies.

### Transactions

- **Nested under the month:** `api/MonthlySummaries/{monthlySummaryId}/Transactions` (`GET` paged listing, `GET {id}`, `POST` → 201, `PUT {id}`, `DELETE {id}` → 204). `TransactionService` always loads the month with `(monthlySummaryId, UserId)` first (`MonthlySummary.NotFound` otherwise) and then works only inside that month, so another user's transaction is never reachable. Transactions have no `UserId` of their own; ownership comes from the month.
- **Rules** (`Transaction.Create`/`Update`, codes in `TransactionErrors`):
  - the type must be a defined `MoneyFlowType` (`Transaction.InvalidType`);
  - the description is trimmed, required, at most 80 characters;
  - the amount is always positive (the type gives the direction), with at most 2 decimals and at most `TransactionAmount.MaxValue` (fits `numeric(14,2)`);
  - the date is a `DateOnly` (`date` column) that must fall inside the month's year/month (`Transaction.DateOutsideMonth`). A transaction never moves to another month.
  - the category is required and must belong to the user: the service loads it with `ICategoryRepository.GetByIdAsync(id, userId)` and returns `Category.NotFound` otherwise.
- **Derived columns:** `NormalizedDescription` (trigram GIN index, search like Categories) and `SignedAmount` (income positive, expense negative). `SignedAmount` exists because EF can't do arithmetic on a converted value object: the listing sorts by it (`Amount` sort = signed value) and the totals are summed from it. `ExecuteUpdateAsync` must set both together with `Amount`/`Type`/`Description`.
- **Totals are recalculated on every write** (`TransactionRepository.WriteAndRecalculateTotalsAsync`), inside one database transaction: lock the month row with `SELECT ... FOR UPDATE`, write, `GROUP BY Type SUM(SignedAmount)`, `ApplyTotals`, then `ExecuteUpdateAsync` the month's `TotalIncome`/`TotalExpense`/`BalanceAmount`. The lock serializes concurrent writes to the same month; summing (instead of adding/subtracting deltas) means the totals can't drift. The mock does the same recalculation on `MonthlySummarySingleton`.
- **Listing:** `?page=&pageSize=&type=All|Expense|Income&categoryId=&search=&sortBy=Date|Description|Category|Amount&sortDirection=` (default `Date`/`Desc`). Every sort ends with `TransactionDate`, `Id` in the same direction. `Category` sorts by the joined category's `NormalizedName`.
- **Category FK is `NO ACTION`**, not `RESTRICT`: deleting a user cascades to both its categories and (through its months) its transactions in the same statement, and `NO ACTION` only checks the FK at the end of it. Deleting a category that transactions still use is blocked earlier by `CategoryService.DeleteAsync` (`ITransactionRepository.ExistsByCategoryAsync` → `Category.InUse`, 409).

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
