# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

LifeManager is a personal finance / life management API (.NET 10, C#, PostgreSQL via EF Core + Npgsql). It is consumed by the sibling React SPA `LifeManagerFront` (`../LifeManagerFront`, served at `https://localhost:5173`).

Current state:
- **Exposed over HTTP:** Auth (two-step register with e-mail confirmation, password reset by e-mail, login/refresh/logout), Users (`GET /api/Users/Me` → `{ name }` of the authenticated user, shown in the frontend's Header menu), UserPreferences (get/save theme + language), Categories (full CRUD with a paged, searchable listing), MonthlySummaries (create a month, month details, and a paged listing filtered by year/balance and sortable by period, income, expenses, investment or balance), Transactions (full CRUD inside a month, which keeps the month's totals up to date; types Expense, Income and Investment), RecurringTransactions (monthly transactions posted automatically by a background job), BudgetPlans (one budget per month: a total split into percent slices per expense category plus one investment slice), FinanceDashboard (one aggregated read of a period of months for the frontend's dashboard, goals included), Habits (the gamified habits module: habits, check-ins, relapses, player profile, stats and ledger), Rewards (the coin shop) and StatementImports (bank statement drafts reviewed and then posted as transactions across any months and years, with category suggestions learned from the user).
- **Background work:** four hourly jobs, recurring transactions posting (`RecurringTransactionsRunner`, see RecurringTransactions), the habits' day close (`HabitsDayCloseRunner`, see Habits), the cleanup of abandoned import drafts (`StatementImportsCleanupRunner`, see StatementImports) and the auth cleanup (`AuthCleanupRunner`, see Auth). Locally they run in-process as hosted services; in production a Cloud Run Job runs them (see "Background jobs and deploy").

## Documentation

- `README.md`: the project summary and the step-by-step local setup (Postgres via the root `docker-compose.yml`, user-secrets, dev certificate, migrations), for people (in Portuguese).
- `docs/deploy.md`: production on Google Cloud (architecture, costs, one-time setup, migrations, `deploy/deploy-api.ps1`), in Portuguese. **When a change adds a configuration key, a secret, a background job or anything the deploy depends on, update it in the same change.**
- `docs/regras-de-negocio.md`: the business rules in product language, with no class names, plus an appendix of every error code (in Portuguese). **When a change adds or changes a business rule, a limit/number (e.g. `GameRules`) or an error code, update this file in the same change.**
- The screen-by-screen user guide lives in the frontend repo (`../LifeManagerFront/docs/fluxo-de-telas.md`).
- This file stays the technical reference; don't duplicate it in those docs.

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

Run the background jobs once and exit (what the production Cloud Run Job does):

```
dotnet run --project LifeManager.WebApi -- --run-jobs
```

Apply the pending migrations and exit (what the production `lifemanager-migrate` Cloud Run Job does before each deploy):

```
dotnet run --project LifeManager.WebApi -- --migrate
```

Build the production image (context = repo root):

```
docker build -f LifeManager.WebApi/Dockerfile -t lifemanager-api .
```

Migrations (EF Core tools, `dotnet ef`): migrations live in `LifeManager.Infrastructure/Migrations`, and the startup project is the WebApi:

```
dotnet ef migrations add <Name> --project LifeManager.Infrastructure --startup-project LifeManager.WebApi
dotnet ef database update --project LifeManager.Infrastructure --startup-project LifeManager.WebApi
```

Required configuration keys, read from `IConfiguration` (user secrets or environment variables); startup throws if any is missing:
- `lifeManagerConnectionString`: the Postgres connection.
- `accessTokenSecretKey` and `refreshTokenSecretKey`: the JWT signing secrets.

- `frontendBaseUrl`: the frontend's origin, used to build the links in the auth e-mails (`FrontendLinks`). `appsettings.Development.json` sets `https://localhost:5173`.
- `brevoApiKey` and `emailSenderAddress` (a sender verified in Brevo): required outside Development. Without `brevoApiKey`, Development uses `LoggingEmailSender` (writes each e-mail, link included, to the console); any other environment throws at startup. With it, `emailSenderAddress` is required.

Optional:
- `businessTimeZone` (IANA id, default `America/Sao_Paulo`), the time zone of "today" for business rules (`AppClock`). The host needs time zone data (tzdata); an unknown id throws on first use.
- `emailSenderName` (default `LifeManager`), the sender name of the auth e-mails.

Note: `LifeManager.Tests` (singular) is a leftover scaffold project. It is not referenced in `LifeManager.slnx` and contains no code beyond its `.csproj`. The real test suites are `LifeManager.Domain.Test` and `LifeManager.Application.Test`.

## Architecture

Layered/Clean Architecture split across four projects, referencing inward only. Every layer uses the same feature folders (`Auth`, `Users`, `UsersPreferences`, `Categories`, ...), plus a `Shared/` folder for cross-cutting building blocks.

- **LifeManager.Domain**
  - Holds entities, value objects, domain errors and repository interfaces, with no external dependencies.
  - Each feature folder has `ValueObjects/`, `Errors/` and (where relevant) `Interfaces/`.
  - `Shared/` holds `Results/` (Result pattern), `Paging/` (`PageRequest`, `PagedList<T>`, `PagingErrors`), `Text/` (`SearchText`), `Enums/` (`MoneyFlowType`, `SortDirection`) and `ValueObjects/` (`YearMonth`: `yyyy-MM`, years 2000–2100, `Ordinal`/`AddMonths`/`FirstDay`/`LastDay`; used by the dashboard, recurrences and budget plans).
  - `InternalsVisibleTo` exposes internals to Infrastructure, so value objects' `internal static FromPersistence(...)` rehydrate from the database without re-validating. `LifeManager.Application.Test` and `LifeManager.Domain.Test` also see them, to seed stored state that the validating factories can't create.
- **LifeManager.Application**
  - Application services orchestrate domain logic: `AuthService`, `TokenService`, `RegistrationService`, `PasswordResetService`, `AuthCleanupService`, `UserService`, `UserPreferencesService`, `CategoryService`, `MonthlySummaryService`, `TransactionService`, `RecurringTransactionService`, `RecurringTransactionPostingService`, `BudgetPlanService`, `FinanceDashboardService`, `EnvironmentVariableService`.
  - `Shared/Time/AppClock` gives "today" (`DateOnly`) in `businessTimeZone`, backed by `TimeProvider` (registered as `TimeProvider.System`; tests replace it). New code that needs the date uses it instead of `DateTimeOffset.UtcNow`.
  - Each feature has its DTOs; `Shared/DTOs/` holds `PagedResponseDto<T>`.
  - Depends on `LifeManager.Domain` only.
  - Services are registered as scoped in `DI/DependencyInjection.cs` (`AddApplicationServices`).
- **LifeManager.Infrastructure**
  - Persistence with EF Core + Npgsql. `Postgres/LifeManagerDbContext.cs` has the DbSets `Users`, `RefreshTokens`, `EmailCooldowns`, `PendingRegistrations`, `PasswordResetTokens`, `UserPreferences`, `Categories`, `MonthlySummaries`, `Transactions`, `RecurringTransactions`, `BudgetPlans` (its slices are an owned collection in `BudgetPlanSlices`), the Habits and Rewards sets, `StatementImports`, `StatementImportRows` and `CategorizationRules`, and declares the `pg_trgm` extension.
  - One `IEntityTypeConfiguration<T>` per entity lives in `Postgres/Configurations/` and is applied via `ApplyConfigurationsFromAssembly`. Value objects are mapped with `HasConversion(vo => vo.Value, v => X.FromPersistence(v))`.
  - Naming is EF's default PascalCase (tables `"Categories"`, columns `"UserId"`), so any raw SQL must quote identifiers.
  - Repositories live in feature folders (`Users/`, `Auth/`, `UsersPreferences/`, `Categories/`, `MonthlySummaries/`, `Transactions/`). Shared query helpers live in `Postgres/Extensions/`.
  - Registered in `DI/DependencyInjection.cs` (`AddInfrastructureServices(connectionString)`).
- **LifeManager.WebApi**
  - ASP.NET Core host. Controllers live in feature folders (`Auth/Controllers`, `UsersPreferences/Controllers`, `Categories/Controllers`, `MonthlySummaries/Controllers`, `Transactions/Controllers`, `RecurringTransactions/Controllers`, `BudgetPlans/Controllers`, `FinanceDashboard/Controllers`) under `[Route("api/[controller]")]`; `TransactionsController` is nested under its month (`api/MonthlySummaries/{monthlySummaryId}/[controller]`).
  - Background jobs live next to their feature (`RecurringTransactions/Jobs/`, `Habits/Jobs/`) and are registered in `AddApiServices` — see "Background jobs and deploy".
  - `Program.cs` wires controllers (enums serialized as strings via `JsonStringEnumConverter`), OpenAPI (Development only), Infrastructure, Application, and `DI/DependencyInjection.cs` (`AddApiServices`: JWT bearer auth, the `AllowFrontend` CORS policy for `https://localhost:5173` with credentials, forwarded headers and the jobs). With `--run-jobs` it runs the jobs once and exits before the middleware (`RunJobsMode.cs`); with `--migrate` it applies the pending migrations and exits (`RunMigrationsMode.cs`).
  - Middleware order: forwarded headers → `ExceptionHandlingMiddleware` → CORS → rate limiter → HTTPS redirection → authentication → authorization. The rate limiter comes after CORS so a 429 still carries the CORS headers in local dev. Forwarded headers (`X-Forwarded-Proto`/`-For`, any proxy) come first because Cloud Run terminates TLS; without them `UseHttpsRedirection` would see plain HTTP.

### Background jobs and deploy

- **Runners** (`RecurringTransactionsRunner`, `HabitsDayCloseRunner`, `StatementImportsCleanupRunner`, `AuthCleanupRunner`, singletons) hold one run: list the due work in one scope, then one scope per unit (recurrence / user), logging and skipping failures. The schedule is the cursor in the database, so a missed or interrupted run loses nothing.
- **Locally**, the hosted services `RecurringTransactionsJob`/`HabitsDayCloseJob` call their runner at startup and every hour (`PeriodicTimer`). They're registered unless `backgroundJobs:enabled` is `false`.
- **In production** (Google Cloud, `docs/deploy.md`), the API runs on Cloud Run scaled to zero with `backgroundJobs__enabled=false` (CPU only exists during requests). Cloud Scheduler triggers, every hour, a Cloud Run Job running the same image with `--run-jobs` (`RunJobsMode`: recurring transactions, the day close, the import drafts cleanup, the auth cleanup, then exit 0). The jobs build the same services as the API (`AddApiServices`), so `deploy/deploy-api.ps1` gives the service and both jobs the same secrets and `frontendBaseUrl`.
- **Migrations stay additive (expand/contract).** `--migrate` (`RunMigrationsMode`) applies them while the previous revision is still serving, so that revision must keep working on the new schema: add columns/tables first, and drop or rename only in a later change, once no deployed code reads the old shape. `MigrateAsync` also fails on a model change that has no migration, which stops the deploy.
- **Same origin:** Firebase Hosting serves the frontend and rewrites `/api/**` to the Cloud Run service. That's why the refresh cookie is named `__session`, the only cookie Hosting forwards. CORS only matters for local dev.
- **Image:** `LifeManager.WebApi/Dockerfile` (context = repo root; the `aspnet` image ships tzdata for `businessTimeZone`), built by Cloud Build (`cloudbuild.yaml`) from `deploy/deploy-api.ps1`. Every push on `main` runs it from GitHub Actions (`.github/workflows/deploy.yml`: `dotnet test`, then the script under the `production` environment, authenticated by Workload Identity Federation; the run-wide `concurrency` keeps deploys in merge order); running it by hand is the fallback. The script then runs the Cloud Run Job `lifemanager-migrate` (same image, `--migrate`) and waits for it, and only if it succeeds updates the service and points the hourly job at the new image.

Test projects mirror the layer they test 1:1 (`LifeManager.Domain.Test` → Domain, `LifeManager.Application.Test` → Application) and reference only that layer (plus Domain, transitively). **There are no Infrastructure/WebApi tests and nothing runs against a real Postgres**, so repository queries, EF mappings and migrations are only verified by running the API.

### HTTP layer conventions

- **Result → HTTP status:** services return `Result`/`Result<T>`, and controllers turn them into responses with `.Match(...)` (`WebApi/Extensions/ResultExtensions.cs`).
  - `Validation` → 400, `Conflict` → 409, `NotFound` → 404, `Unauthorized` → 401, anything else → 500. The one exception is `Auth.TooManyRequests` (429), written by the rate limiter, not by a service.
  - The body is the `Error` (`{ code, message, type }`), which the frontend maps by `code`.
- **Current user:** authenticated endpoints are `[Authorize]` and read it with `User.GetUserId()` (`WebApi/Extensions/ClaimsExtensions.cs`, from the `NameIdentifier` claim). Every query is scoped by that `UserId`; the user is never taken from the request body or route.
- **`ExceptionHandlingMiddleware`:**
  - Turns unhandled exceptions into 500 (or 400 for `BadHttpRequestException`) with an `Error` body.
  - Swallows `OperationCanceledException` when `RequestAborted` fired and only logs it at Information level. The frontend aborts GET requests (`AbortController`) when it no longer needs them, so these logs are expected.

### Auth

- **E-mail port:** `IEmailSender` (`Domain/Auth/Interfaces`, `EmailMessage` with HTML and text bodies) → `Infrastructure/Emails/BrevoEmailSender` (typed `HttpClient`, POST `https://api.brevo.com/v3/smtp/email`, 10 s timeout) or `LoggingEmailSender` in Development without `brevoApiKey` (see the config keys). A failed send is logged and returns `AuthErrors.EmailNotSent`, which the flows ignore: their answer never depends on the e-mail going out. Bodies come from `Application/Auth/Emails/AuthEmailTemplates` (pt-BR/en, every value HTML-encoded; the confirmation has no name, since whoever fills the form types it, while the other two use the account's stored name); links from `FrontendLinks` (`/auth/set-password?token=`, `/auth/reset-password?token=`, `/auth/forgot-password`).
- **E-mail tokens:** `EmailTokenGenerator.Generate()` (32 random bytes, Base64Url) returns the token for the link and its `EmailTokenHash` (SHA-256, hex). Only the hash is stored; the token travels in the link and then in request bodies, never in an API URL. Unknown, expired and used tokens all return `Auth.InvalidEmailToken` (400). The token is checked before the password, so a dead link is reported whatever was typed.
- **Cooldown:** `IEmailCooldownRepository.TryReserveAsync(email, EmailKind, now)` (table `EmailCooldowns`, key = lowercase e-mail + kind) is an atomic conditional upsert (`INSERT … ON CONFLICT DO UPDATE … WHERE LastSentAt <= now - 60 s RETURNING 1`): each kind (`Confirmation`, `AccountExists`, `PasswordReset`) goes to an address at most once per `EmailCooldown.Duration`. Inside it the flow changes nothing and still succeeds, so the link already sent stays valid.
- `POST /api/Auth/Register` `{ email, name, language?, theme? }` → 202 (`RegistrationService.RegisterAsync`), the same for any valid name and e-mail:
  - E-mail with an account: the "you already have an account" e-mail (in the account's language, with the forgot-password link), nothing stored.
  - Otherwise: a `PendingRegistration` (table `PendingRegistrations`, unique `Email` and `TokenHash`; name, language/theme defaulting to `PTBR`/`Light`, token hash, `ExpiresAt` = +24 h) is created or renewed (`SaveAsync` upserts on the e-mail, so a new sign-up replaces the token and the old link dies) and the confirmation e-mail goes out in the sign-up's language.
- `POST /api/Auth/Register/Verify` `{ token }` → 200 `{ email, name }`, for the set-password page.
- `POST /api/Auth/Register/Complete` `{ token, password }` → 201: `IPendingRegistrationRepository.CompleteAsync`, in one database transaction, deletes the pending row by id **and** token hash (a renewed sign-up loses the race) and stores the user, their preferences and `DefaultCategories.CreateFor(userId, language)` (`Domain/Categories/DefaultCategories.cs`, 12 names per language) through `Infrastructure/Users/UserStarterData`. A unique violation on `Users.Email` (the e-mail became an account meanwhile) returns `Auth.InvalidEmailToken`. The default categories are ordinary rows of the user (no global categories, no default flag); existing users never got them. No automatic login: the frontend goes back to the login.
- `POST /api/Auth/ForgotPassword` `{ email }` → 202 (`PasswordResetService.RequestAsync`): only an e-mail with an account (and outside the cooldown) gets a `PasswordResetToken` (table `PasswordResetTokens`, FK `CASCADE` to the user, unique `TokenHash`, `ExpiresAt` = +1 h, `UsedAt`) and the e-mail in the account's language. `AddReplacingUnusedAsync` deletes the user's unused links first. An e-mail without an account gets nothing, which makes that answer a bit faster (accepted, see `docs/regras-de-negocio.md`).
- `POST /api/Auth/ResetPassword/Verify` `{ token }` → 200 `{ email }`; `POST /api/Auth/ResetPassword` `{ token, password }` → 204: `IPasswordResetTokenRepository.UseAsync`, in one database transaction, marks the link used with a conditional `ExecuteUpdateAsync` (`UsedAt IS NULL AND ExpiresAt > now`, so two concurrent resets can't both win), sets `Users.PasswordHash` and revokes every active refresh token of the user, so the password never changes while the old sessions stay open.
- **Rate limit** (`WebApi/Auth/AuthRateLimits.cs`, `[EnableRateLimiting]` on the actions): fixed windows per `RemoteIpAddress` (after the forwarded headers), `EmailRequests` 10 per 10 min on Register and ForgotPassword, `EmailTokens` 20 per 10 min on the Verify, Complete and ResetPassword calls. `OnRejected` writes 429 with `Auth.TooManyRequests`, a `Retry-After` header and a warning log with the IP.
- **Cleanup:** `AuthCleanupService` (hourly `AuthCleanupRunner`/`AuthCleanupJob`, last in `--run-jobs`) deletes pending sign-ups expired more than `PendingRegistration.RetentionDays` (7) ago, reset links expired or used more than `PasswordResetToken.RetentionDays` (7) ago and cooldowns older than `EmailCooldown.Retention` (1 day).
- `POST /api/Auth/Login`:
  - Returns `{ accessToken }`.
  - Sets the refresh token as an `HttpOnly`, `Secure` cookie `__session` (path `/api/Auth`; the name is the one Firebase Hosting forwards, see "Background jobs and deploy"). The cookie expires together with the token (`LoginResponseDto.RefreshTokenExpiresAt`).
- `POST /api/Auth/Refresh`:
  - Returns 200 `{ accessToken }` and sets a new `__session` cookie.
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

- **Feature folders as bounded contexts:** `Users`, `Auth`, `UsersPreferences`, `Categories`, `Transactions`, `MonthlySummaries`, `RecurringTransactions` and `BudgetPlans` each own their entity, value objects, errors and repository interface. Cross-context references go through IDs (e.g. `Category.UserId`, `Transaction.MonthlySummaryId`, `Transaction.CategoryId`); when a read needs data from another context (a transaction's category name), the repository joins and returns a read model (`TransactionListItem`).
- **Entities** (`User`, `RefreshToken`, `UserPreferences`, `Category`, `Transaction`, `MonthlySummary`, `RecurringTransaction`, `BudgetPlan`) have identity (`Id`) and encapsulate their own invariants.
  - Private constructors force construction through a validating `static Create(...)` factory.
  - Mutation happens only through intention-revealing methods (`AssignId`, `RevokeToken`, `Rename`) rather than public setters.
- **Value objects** (`Email`, `UserName`, `PasswordHash`, `PlainPassword`, `CategoryName`, `TransactionAmount`, `RefreshTokenHash`, id types like `UserId`/`CategoryId`, etc.) are immutable, validate themselves in `Create`, and implement structural `Equals`/`GetHashCode`. They are the primitives that make illegal states unrepresentable instead of passing raw strings/decimals around.
- **Domain errors as part of the model:** each context's `*Errors` static class (`UserErrors`, `AuthErrors`, `CategoryErrors`, ...) enumerates the domain's known failure modes by name (e.g. `AuthErrors.InvalidEmailToken`, `CategoryErrors.NameAlreadyExists`). Failure cases are discoverable and testable like any other part of the ubiquitous language, not ad-hoc exception messages. Error codes (`"Category.NameAlreadyExists"`) are part of the API contract: the frontend maps them to form fields and translated messages, so don't rename them casually.
- **Repository interfaces** live in the Domain layer, expressed in domain terms, and are implemented in `LifeManager.Infrastructure` (a standard DDD/hexagonal port-adapter split).

### Result pattern (no exceptions for expected failures)

The codebase is mid-migration to a `Result`/`Result<T>` pattern (`LifeManager.Domain/Shared/Results/`) for anything that can fail validation, mirroring a typical Railway-Oriented-Programming style:

- `Error` is a record with a `Code`, `Message`, and `ErrorType` (`Validation`, `NotFound`, `Unauthorized`, `Failure`, `Conflict`), created via static factories (`Error.Validation(...)`, `Error.Conflict(...)`, etc.).
- `Result` / `Result<T>` have implicit conversions from `Error` and from `T`, so factory methods can `return SomeErrors.Whatever;` or `return new Thing(...)` directly instead of throwing.
- `ResultExtensions` provides `Map`, `Bind`, and `Tap` for chaining `Result<T>` operations functionally (see `User.Create` and `TokenService.GenerateTokensAsync` for the chaining style). The extensions are synchronous, so async steps (repository calls) go after the chain, in the early-return style of `CategoryService`.
- Every context (`Users`, `Auth`, `UsersPreferences`, `Categories`, `MonthlySummaries`, `Transactions`, `RecurringTransactions`, `BudgetPlans`) uses this pattern; there is no `DomainException` anymore. New code must not throw for expected failures.

### Categories (reference flow for new features)

- **Default categories:** every new user starts with the `DefaultCategories` copies made when the sign-up completes (see Auth). Nothing tells them apart from categories the user created.

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
- **Totals:** `TotalIncome`, `TotalExpense` and `TotalInvestment` (all VOs, never negative). **Balance = income − expense − investment**: an investment is money set aside, so it leaves the account like an expense but is kept apart from spending. A redemption isn't income: statement imports leave detected redemptions out (the user may include one as Income).
- **Balance column:** `Balance` (VO) is derived and ignored by EF; `BalanceAmount` is its persisted copy (same idea as `Category.NormalizedName`) so the listing filters (`Positive` = ≥ 0, `Negative` = < 0) and sorts by it in SQL. Anything that changes the totals must also update `BalanceAmount` (including in `ExecuteUpdateAsync`).
- **Details:** `GET /api/MonthlySummaries/{id}` returns `MonthlySummaryDetailsResponseDto`: the totals plus `IncomeCount`/`ExpenseCount`/`InvestmentCount` (`ITransactionRepository.CountByTypeAsync`) and `PreviousId`/`NextId`, the user's closest months before and after (`IMonthlySummaryRepository.GetNeighborsAsync`, which reads only the user's month keys because Year/Month are value objects and can't be compared in SQL). Create and the listing still return `MonthlySummaryResponseDto`.
- **Totals** only change through transactions: `MonthlySummary.ApplyTotals(income, expense, investment)` replaces the three totals and keeps `BalanceAmount` in sync (see Transactions).
- **Listing:** `GET /api/MonthlySummaries?page=&pageSize=&year=&balance=All|Positive|Negative&sortBy=Period|TotalIncome|TotalExpense|TotalInvestment|Balance&sortDirection=` (default `Period`/`Desc`, newest first). Every sort ends with Year, Month, Id in the same direction. `GET /api/MonthlySummaries/Years` returns the user's distinct years (newest first) for the year filter.
- **Tests:** `MonthlySummary.FromPersistence(...)` (internal) rehydrates a stored summary without the creation rules, so tests can seed past years and non-zero totals that `Create` can't produce; the repository mock also uses it for its detached copies.

### Transactions

- **Nested under the month:** `api/MonthlySummaries/{monthlySummaryId}/Transactions` (`GET` paged listing, `GET {id}`, `POST` → 201, `PUT {id}`, `DELETE {id}` → 204). `TransactionService` always loads the month with `(monthlySummaryId, UserId)` first (`MonthlySummary.NotFound` otherwise) and then works only inside that month, so another user's transaction is never reachable. Transactions have no `UserId` of their own; ownership comes from the month.
- **Rules** (`Transaction.Create`/`Update`, codes in `TransactionErrors`):
  - the type must be a defined `MoneyFlowType` (`Transaction.InvalidType`);
  - the description is trimmed, required, at most 80 characters;
  - the amount is always positive (the type gives the direction), with at most 2 decimals and at most `TransactionAmount.MaxValue` (fits `numeric(14,2)`);
  - the date is a `DateOnly` (`date` column) that must fall inside the month's year/month (`Transaction.DateOutsideMonth`). A transaction never moves to another month.
  - the category is required and must belong to the user: the service loads it with `ICategoryRepository.GetByIdAsync(id, userId)` and returns `Category.NotFound` otherwise.
- **Derived columns:** `NormalizedDescription` (trigram GIN index, search like Categories) and `SignedAmount` (income positive; expense and investment negative, since both leave the account). `SignedAmount` exists because EF can't do arithmetic on a converted value object: the listing sorts by it (`Amount` sort = signed value) and the totals are summed from it. `ExecuteUpdateAsync` must set both together with `Amount`/`Type`/`Description`.
- **Totals are recalculated on every write** (`TransactionRepository.WriteAndRecalculateTotalsAsync`), inside one database transaction: lock the month row with `SELECT ... FOR UPDATE`, write, `GROUP BY Type SUM(SignedAmount)`, `ApplyTotals`, then `ExecuteUpdateAsync` the month's `TotalIncome`/`TotalExpense`/`TotalInvestment`/`BalanceAmount`. The lock serializes concurrent writes to the same month; summing (instead of adding/subtracting deltas) means the totals can't drift. The mock does the same recalculation on `MonthlySummarySingleton`.
- **Listing:** `?page=&pageSize=&type=All|Expense|Income|Investment&categoryId=&search=&sortBy=Date|Description|Category|Amount&sortDirection=` (default `Date`/`Desc`). Every sort ends with `TransactionDate`, `Id` in the same direction. `Category` sorts by the joined category's `NormalizedName`.
- **Category FK is `NO ACTION`**, not `RESTRICT`: deleting a user cascades to both its categories and (through its months) its transactions in the same statement, and `NO ACTION` only checks the FK at the end of it. Deleting a category that transactions or recurring transactions still use is blocked earlier by `CategoryService.DeleteAsync` (`ExistsByCategoryAsync` on both repositories → `Category.InUse`, 409).
- **Totals helper:** the lock + recalculation lives in `Infrastructure/Transactions/MonthlySummaryTotals.cs` (`LockAsync`, `RecalculateAsync`, both inside the caller's database transaction), shared by `TransactionRepository` and the recurring posting.
- **Recurring link:** `Transaction.RecurringTransactionId` (nullable) is the recurrence that posted it (`Transaction.CreateFromRecurrence`); it is returned as `recurringTransactionId` so the frontend can mark it. FK `SET NULL`: deleting the recurrence keeps what it posted. A unique partial index on `(RecurringTransactionId, MonthlySummaryId)` is the last guard against posting a recurrence twice in a month.
- **Import link:** `Transaction.StatementImportId` (FK `SET NULL`, removing the import from the history keeps its transactions) and `ExternalId` (the bank line, `ImportExternalId`), both set only by `Transaction.CreateFromImport`. A unique partial index on `(MonthlySummaryId, ExternalId)` is the last guard against importing the same bank line twice. `ITransactionRepository.GetForDuplicateCheckAsync` and `GetCategoryHistoryAsync` serve the imports (see StatementImports).

### RecurringTransactions

- **Endpoints:** `api/RecurringTransactions`: `GET` paged listing, `GET {id}`, `POST` → 201, `PUT {id}`, `POST {id}/Pause`, `POST {id}/Resume`, `DELETE {id}` → 204 (posted transactions are kept). Owned by `UserId` directly.
- **Model** (`Domain/RecurringTransactions/RecurringTransaction.cs`): type/category/amount/description reuse the transaction value objects and error codes; `DayOfMonth` (`RecurrenceDay`, 1–31; a shorter month falls on its last day); `StartMonth`, optional `EndMonth`; `IsActive`; and the **posting cursor** `NextMonth`, the first month not posted yet. `NextOccurrenceDate` is its persisted derived copy (null once past `EndMonth`), indexed (partial, `WHERE "IsActive"`) for the due query. Months are stored as their first day (`date`). `Status` = Finished (no next occurrence) / Paused / Active.
- **Rules** (`RecurringTransactionErrors`): the start can't be before the current month (`StartInPast`, no backfilling history) and can only change while the recurrence hasn't started (`StartLocked`); `EndBeforeStart`; `Pause`/`Resume` (`AlreadyPaused`/`NotPaused`). Resuming skips the paused months (the cursor jumps to the current month). Updates change only the next occurrences.
- **Posting** (`RecurringTransactionPostingService.PostDueOccurrencesAsync`): while the recurrence is due (`IsActive && NextOccurrenceDate <= AppClock.Today()`), get or open the occurrence's month (`MonthlySummary.OpenForRecurringPosting`, which skips the current-year rule so a catch-up across New Year works; `IMonthlySummaryRepository.AddIfMissingAsync` is an `INSERT … ON CONFLICT DO NOTHING`), build the transaction, advance the cursor and call `TryPostOccurrenceAsync`. That runs in one database transaction: a **compare-and-swap** on the cursor (`WHERE NextMonth = expected AND IsActive`, which also row-locks the recurrence), the insert, and the month's totals recalculation. If the CAS hits 0 rows (another run posted it, or it was paused/deleted) nothing is written and posting stops quietly. Missed months are caught up one by one.
- **User edits use the same CAS** (`TryUpdateAsync(…, expectedNextMonth)`): if the job moved the cursor in between, the edit returns `RecurringTransaction.ChangedConcurrently` (409) instead of overwriting it.
- **Saving posts right away:** create, update and resume call the posting service, so an occurrence whose day already passed this month shows up without waiting for the job.
- **Job** (`WebApi/RecurringTransactions/Jobs/RecurringTransactionsRunner.cs`; run every hour by the `RecurringTransactionsJob` hosted service locally, or by the Cloud Run Job in production — see "Background jobs and deploy"): it reads up to `DueBatchSize` (500) due ids in one scope, then posts each in its own scope (the `DbContext` is scoped, the runner a singleton), logging and skipping failures; the next run retries them. The schedule is the cursor in the database, so the runner holds no state and several instances can run it safely.
- **Listing:** `?page=&pageSize=&type=All|Expense|Income|Investment&status=All|Active|Paused|Finished&search=&sortBy=NextOccurrence|Description|Amount|Day&sortDirection=` (default `NextOccurrence`/`Asc`). Every sort ends with `NormalizedDescription`, `Id`; finished recurrences (null next occurrence) come last ascending and first descending (PostgreSQL's NULL order, mirrored in the mock).

### BudgetPlans

- **Endpoints:** `GET api/BudgetPlans/{month}` (`yyyy-MM`; the month's plan with each slice's actual amount, or `exists: false`), `PUT api/BudgetPlans/{month}` (`{ total, slices: [{ type, categoryId?, percent }] }`: creates or **replaces the whole** month's plan) → 200 with the same response as the GET, `DELETE api/BudgetPlans/{month}` → 204 (`BudgetPlan.NotFound` when the month has none).
- **Model** (`Domain/BudgetPlans/`): `BudgetPlan` = `UserId`, `MonthStart` (a plain `date` column holding the month's first day, so months range-compare in SQL; `Month` exposes it as `YearMonth`), `Total` (`BudgetTotal`, money rules with `BudgetPlan.Total*` codes) and `Slices`. A `BudgetSlice` is `Type` (`Expense`, which needs a category, or `Investment`, at most one and without category), `CategoryId` and `Percent` (`SlicePercent`: > 0, ≤ 100, 2 places). `Create` validates the whole plan (sum ≤ 100 → `OverAllocated`, `DuplicateCategory`, `InvalidInvestmentSlice`, `ExpenseSliceWithoutCategory`). Derived, not stored: `AmountOf(total)` (rounded to cents, half away from zero), `FreePercent`, `FreeAmount` (total − the slices' rounded amounts, so it closes with the total), `InvestmentSlice`, `SpendingLimit` (total − the investment slice), `IsMetBy` and `ProgressOf` (divides by the unrounded share, never zero).
- **Persistence:** unique `(UserId, MonthStart)`. Slices are an `OwnsMany` collection in their own table `BudgetPlanSlices` (shadow `Id` and `BudgetPlanId`, unique `(BudgetPlanId, CategoryId)`, which leaves the investment slice out since NULLs are distinct). `BudgetPlanService.SaveAsync` always hands the repository a new, validated plan; `BudgetPlanRepository.SaveAsync` loads the month's stored plan tracked (by user and month) and adds the new one when there is none, or calls `ReplaceWith` on it (same user and month only, keeping its id), so EF deletes the old slices and inserts the new ones. Reads come with the user's category names (`BudgetPlanListItem`).
- **Month view** (`BudgetPlanService.GetAsync`): an aggregated read, like the dashboard. Actual amounts come from `IFinanceDashboardRepository.GetCategoryMonthTotalsAsync` for the month: an expense slice against its category, the investment slice against everything invested, and `FreeActual` = spending in categories without a slice. `CopySource` = the latest earlier month with a plan (`GetLatestMonthBeforeAsync`); the frontend copies it into its draft, there is no copy endpoint. Each month is independent: nothing is versioned.
- **Category FK is `CASCADE`**: a slice means nothing without its category, so deleting a category deletes its slices in every month (and doesn't block the deletion).
- **History:** migration `ReplaceBudgetsWithBudgetPlans` converted the old versioned goals (`Budgets` table) into one plan per month, from each user's first month with a goal through the month it ran (rule in its comment and in `docs/regras-de-negocio.md`), then dropped the table. Its `Down` recreates `Budgets` empty.

### FinanceDashboard goals

- The dashboard response also has `budgets` (`DashboardBudgetsDto`), for the period only (no comparison). `IBudgetPlanRepository.GetOverlappingAsync` reads the period's plans, and each month is evaluated with its own plan against the same rows the rest of the dashboard uses, so no extra transaction query and categories beyond the top 8 still count.
- `months` (aligned with the response's months): each month's spending limit (`SpendingLimit`: total − investment slice, the free part included) and investment goal (the investment slice; null without one), and whether each was met. `expense`/`investment`: those goals summed over the months that had them, with `monthsWithGoal`/`monthsAchieved`. `expenseCategories`: the expense slices by category, most overspent first (`Gap`: summed overspending), top `TopBudgetCategoryCount` (5). `investmentCategories` is always empty (one investment slice per plan), kept for the contract.
- The endpoint still never reads the clock: the running month is counted like any other, and the frontend leaves it out of its "X of N closed months".

### FinanceDashboard

- **Endpoint:** `GET /api/FinanceDashboard?from=yyyy-MM&to=yyyy-MM&comparison=PreviousPeriod|SamePeriodLastYear`.
  - It returns one `FinanceDashboardResponseDto` with everything the dashboard shows:
    - the period and the comparison period;
    - income/expense/investment/balance/saved totals, each with previous value, difference and `ChangeRatio`, plus the period's `SavingsRate` (`DashboardTotals`: `Saved` = income − expense = investment + balance; `SavingsRate` = saved ÷ income, null with no income);
    - one row per month of the period;
    - expense and investment breakdowns by category.
  - The frontend resolves presets ("last 6 months", "this year") into `from`/`to` itself, since only it knows the user's local date. The endpoint never reads the clock.
- **Period rules** (`DashboardPeriod`/`YearMonth` in the Domain, codes in `FinanceDashboardErrors`, all 400):
  - whole months in `yyyy-MM`, years 2000–2100 (`InvalidFrom`/`InvalidTo`);
  - `to ≥ from` (`EndBeforeStart`);
  - at most 36 months (`PeriodTooLong`).
- **Comparison periods** (`DashboardPeriod.ComparisonFor`):
  - `PreviousPeriod` is the same number of months right before.
  - `SamePeriodLastYear` is the same months 12 months back. It's only allowed for periods up to 12 months (`ComparisonTooLong`), so the two never overlap.
  - An undefined value returns `InvalidComparison`. Over HTTP, model binding already rejects an undefined value (e.g. `comparison=7`) with ASP.NET's ProblemDetails 400, so this is only a guard for direct callers of the domain.
- **One grouped query:** `FinanceDashboardRepository` sums the user's transactions per category, type and month.
  - It filters on `TransactionDate` over the comparison start through the period end. Ownership is checked through the month (`MonthlySummaries.Any(... UserId)`).
  - It groups on plain columns (`CategoryId`, `Type`, `TransactionDate.Year/Month`) and sums `SignedAmount`.
  - Category names come from a second query over the user's categories, because grouping by the converted `Name` isn't translated reliably.
  - No extra index is needed. The mock mirrors all of this over the singletons.
- **Assembly in `FinanceDashboardService`:**
  - Months with no transactions are zero-filled.
  - Breakdowns list the top `TopCategoryCount` (8) categories by amount (ties by name, then id). The rest go into `Others`, whose previous amount is the previous total minus the listed ones, so the parts always add up.
  - `ChangeRatio = (current − previous) / |previous|`, rounded to 4 places, and null when the previous value is 0.
  - `Share` is the part of the breakdown total (0 when the total is 0).
  - `MonthlyAmounts` lines up with `Months`.

### Habits

- **Player:** `PlayerProfile` (one per user, created on its first change) holds level, XP, HP, coins and streak freezes; the rules (rewards per difficulty, levels, knockout) are in the static `Domain/Habits/GameRules.cs`. `PlayerWalletService` is the only way to change it: it locks the profile row and appends a `GameLedgerEntry` in the same transaction. `GET api/Habits/Profile` (`HabitProfileController`) returns it, plus `LastKnockout` (the latest `Knockout` ledger entry: its id, day and coins lost, via `IGameLedgerRepository.GetLatestAsync`; null elsewhere, e.g. inside a `WalletChangeDto`) so the frontend can tell the player about a knockout the day close caused while they were away.
- **Habits endpoints:** `api/Habits` (`HabitsController`, ids constrained to `{id:int}` so they don't clash with `Profile`): `GET` paged listing, `GET {id}`, `POST` → 201, `PUT {id}` (everything but the kind), `DELETE {id}` → 204 **archives** (`ArchivedAt`; history is kept), `POST {id}/Restore`.
- **Model** (`Domain/Habits/Habit.cs`): `Name` (`HabitName`, ≤ 60, normalized copy in `NormalizedName`), optional `Description` (≤ 200) and `Trigger` (≤ 120, the "after X, I do Y" cue), `Kind` (`Positive`/`Negative`, fixed once created), `Difficulty`, and the frequency as three columns rebuilt into the `HabitFrequency` value object: `FrequencyType` (`Daily`/`WeekDays`/`TimesPerWeek`), `WeekDays` (the `[Flags] HabitWeekDays` bit mask, Monday = 1 … Sunday = 64; the API sends a `DayOfWeek` list, converted by `HabitWeekDaysMapper`) and `TimesPerWeek` (1–6). Each frequency only allows its own field (`InvalidFrequencyCombination`). Both kinds take any frequency.
- **Frequency of a habit to avoid** (`Negative`; the rules the relapse and clean-day flows must follow): `Daily` = avoided every day; `WeekDays` = avoided on the picked days only, the other days are free (no relapse button, no damage, no coins); `TimesPerWeek` = a weekly allowance, "at most N times a week": up to N relapses a week cost nothing, each one past it costs HP, and a week that closes within it counts for the streak (in weeks) and earns coins. Clean days only count on scheduled days.
- **Game state on the habit:** `StartDate` (`AppClock.Today()` on create), `CurrentStreak`/`LongestStreak` and the day-close cursor `EvaluatedUntil` (starts at `StartDate - 1`). An archived habit can't be updated (`Habit.Archived`, 409); restoring zeroes the current streak and moves the cursor to yesterday, so the archived days are never judged.
- **Names** are unique among the user's **active** habits only: unique partial index `(UserId, NormalizedName) WHERE "ArchivedAt" IS NULL`, checked by `ExistsActiveByNameAsync` on create, rename and restore (`Habit.NameAlreadyExists`).
- **Listing:** `?page=&pageSize=&status=Active|Archived&search=&sortBy=Name|CreatedAt&sortDirection=` (default `Active`, `Name`, `Asc`); every sort ends with `NormalizedName`, `Id`.
- **Check-ins** (`HabitCheckIn`, unique `(HabitId, Date)`; `Status` `Done`/`Missed`/`Frozen`/`Relapse`/`Clean`, only `Done` written by users): `POST api/Habits/{id}/CheckIns` `{ date }` and `DELETE api/Habits/{id}/CheckIns/{date}` → 200 with `HabitCheckInResultDto` (streak + `WalletChangeDto`), served by `HabitCheckInService`. `Habit.EnsureCanCheckIn` allows only an active habit to build (`NotCheckable` for `Negative`), on a day it is scheduled (`NotScheduled`), from `StartDate`, and **only today or yesterday** (`CheckInOutsideWindow`; `StreakCalculator.IsEditable`). `AlreadyCheckedIn` (409) / `CheckInNotFound` (404).
- **Reward:** `GameRules.Reward(difficulty)` coins with `CoinsWithStreakBonus` (the streak already counting this day; a weekly streak's week is worth 7 days), its XP and `HealPerCompletion` HP. A times-per-week habit past its week's target still checks in but earns nothing. The check-in stores what was actually applied (`CoinsAwarded`/`XpAwarded`/`HpAwarded`), so **undo** applies the exact inverse, except that HP is only taken back down to 1 (an undo never knocks out). Ledger entries carry `HabitId` (FK `SET NULL`).
- **Streak** is recalculated from history on every check-in/undo (`StreakCalculator.Current`, pure): days (or Monday–Sunday weeks for times-per-week) going back from today that succeeded (`Done`/`Frozen`); an unscheduled day is skipped, and a day/week that can still be checked in neither counts nor breaks it. `Habit.SetStreak` raises `LongestStreak`.
- **Transaction** (`HabitCheckInRepository`): `PlayerWallet.LockAsync` first (every game write of a user takes that lock, so check-ins never interleave), then the existence check, the success dates, the service's `decide` callback (rules), and the check-in + ledger + profile + habit streak, in one database transaction. The mock mirrors it.
- **Day close** (`HabitEvaluationService`, run every hour by `WebApi/Habits/Jobs/HabitsDayCloseRunner.cs` — through the `HabitsDayCloseJob` hosted service locally or the Cloud Run Job in production — one scope per user, failures logged and retried next run): judges every day up to `today - 2` (`LastClosedDay`; days still checkable are never touched), walking each user's habits **day by day in date order** from their cursor `EvaluatedUntil`. `HabitDayVerdict.Judge` (pure) decides per habit and day:
  - not due / before `StartDate` / a weekly habit's mid-week day → `Skip`;
  - weekly limit (habit to avoid, times per week), on its week's Sunday: relapses ≤ N → `WeekClean`, a `Clean` check-in on every relapse-free day and one reward per clean day (`ApplyCompletion(..., days)`, all on the latest clean day's check-in); over N → `Settled` (each relapse past N already cost HP);
  - already has a check-in (done, relapse) → `Settled`;
  - habit to build, daily or set days, not done → `Missed`: a streak freeze if the player has one (`Frozen` check-in, `FreezeUsed` entry, no damage), otherwise a `Missed` check-in and `GameRules.Damage` (`HabitMissed`);
  - habit to avoid, due day without a relapse → `Clean` check-in rewarded like a check-in (`HabitRewards.ForCompletion`, shared with `HabitCheckInService`; `CleanDay` entry);
  - times-per-week habit to build, on its week's Sunday, short by N → damage × N in one `HabitMissed` entry (no check-in), or one freeze that writes N `Frozen` check-ins on the week's free days; weeks that started before `StartDate` aren't judged.
  - **At most `GameRules.MaxKnockoutsPerClose` (1) knockout per user per run**: later damage in the run is forgiven (the misses are still recorded and still break streaks), so a long absence costs one knockout.
  - **Cursor CAS** (`HabitEvaluationRepository.EvaluateDayAsync`): in one transaction, `PlayerWallet.LockAsync` (same lock order as check-ins), then `UPDATE Habits SET EvaluatedUntil = d WHERE Id = … AND EvaluatedUntil = d - 1 AND ArchivedAt IS NULL`; 0 rows (another run, or archived) → nothing written. Then the week's check-ins and success dates, the rules, and the check-ins + ledger + profile + recalculated streak. Partial index on `EvaluatedUntil WHERE "ArchivedAt" IS NULL` serves the job's lookup.
  - `HabitCheckIn.SuccessStatuses` (`Done`, `Frozen`, `Clean`) are what count for the streak, in both repositories.
- **Streak prizes** (`HabitRewards.ApplyCompletion`, used by check-ins and clean days only; a `Frozen` day keeps the streak but earns nothing): when the streak **passes** (`before < n <= after`, so a check-in of yesterday joining two runs, or a weekly habit's 7-days-a-week jump, still counts) a milestone (7/30/66/100 days → `GameRules.MilestoneBonus` coins, its own `StreakMilestone` ledger entry) or a multiple of `GameRules.FreezeEvery` (a streak freeze, up to `MaxStreakFreezes` held; `PlayerProfile.AddStreakFreeze`). Weekly habits count each streak week as 7 days (`HabitRewards.StreakBonusDays`). The check-in stores the milestone coins in `CoinsAwarded` and `FreezeAwarded`, so an undo gives both back (the freeze only if the player still holds one). `HabitCheckInResultDto` carries `MilestoneDays`, `MilestoneCoins`, `FreezesEarned` for the frontend's toast.
- **`GET api/Habits/Today`** (`HabitTodayDto`): active habits to build scheduled today, plus `YesterdayPending` (scheduled yesterday, not checked in, started before; a weekly one only while that week's target isn't met), each with `Done`, `WeekDoneCount`, `CurrentStreak`/`LongestStreak` and a `CoinsPreview` estimated from the stored streak; plus `LastMissedOn`/`RecentMissCount` (missed days of active habits in the last `RecentMissDays` = 7), so the frontend can welcome the player back after a miss (relapses count as misses). Plus `Avoiding` (`HabitAvoidItemDto`: habits to avoid in force today, with `RelapsedToday`/`RelapsedYesterday`/`CanRelapseYesterday`, `WeekRelapseCount` for a weekly limit and the `DamagePreview` a relapse today would cost) and `FreeToday` (names of set-days habits to avoid on a day off). An aggregated read bounded by the user's habits, so not paged (like the budget plan month view).
- **Relapses** (`HabitRelapseService`): `POST api/Habits/{id}/Relapses` `{ date }` and `DELETE api/Habits/{id}/Relapses/{date}` → 200 with `HabitRelapseResultDto`. `Habit.EnsureCanRelapse`: an active habit to avoid (`NotAvoidable`), on a day it is avoided (a set-days habit's days off: `NotScheduled`), today or yesterday from `StartDate`. One relapse per day (`AlreadyRelapsed` 409 / `RelapseNotFound` 404), stored as a `Relapse` check-in with the HP it cost; `HabitRewards.RelapseDamage`: the difficulty's damage, or within a weekly limit 0 until the relapse past N. An undo applies the exact inverse (a knockout's coins aren't refunded). Both go through `IHabitCheckInRepository.RecordAsync`/`RemoveAsync`, the same profile-locked transaction as check-ins (which pass `Done`).
- **Streak with relapses:** `StreakCalculator.Current(habit, successDates, failedDates, today)` — every service uses this overload. A relapse day breaks the streak on the spot, even today or yesterday; a weekly habit's week needs `WeeklyTarget` successes (`TimesPerWeek` to build, `7 - TimesPerWeek` clean days to avoid) and breaks once it can no longer reach it. Both check-in and evaluation contexts carry `FailedDates` (`HabitCheckInRepository.GetHistoryAsync`).
- **History & stats:** `GET api/Habits/{id}/Stats` (`HabitStatsService` → `HabitStatsDto`, archived habits included) reads `HabitStats` (pure): `Days` gives the last `HeatmapDays` (91) as a `HabitDayState` each — `BeforeStart`, `Off` (a set-days habit's day off), the stored check-in's status, `Pending` (due, still editable, no record; never for weekly or archived habits) or `None`; a failure is never inferred, only read. `Consistency`: daily/set-days habits count the due days of the last 30 with a stored outcome (`Done`/`Clean` kept, `Missed`/`Relapse` not, `Frozen` left out); times-per-week habits count the last 4 **closed** weeks that met the target (to build) or stayed within the limit (to avoid), skipping weeks that started before the habit. Plus `TotalKept` (`CountKeptAsync`, for the 66-day goal). `GET api/Habits/Ledger` (`HabitLedgerController` → `GameLedgerService`) pages the statement newest first (`CreatedAt`, `Id`), optionally one habit's (`habitId`).
- **Rewards shop** (`Domain/Rewards/`, `api/Rewards` → `RewardsController`): rewards the user sets for themselves and buys with coins. `Reward` (`RewardName` ≤ 60 with `NormalizedName`, `Cost` 1–100 000, optional `Icon` ≤ 30 — an id the frontend knows, `ArchivedAt`): `GET` paged (`Active|Archived`, search, sort `Name|Cost` + direction), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` **archives**, `POST {id}/Restore`; names unique among active rewards (partial unique index, `Rewards.NameAlreadyExists`), like habits (`RewardService`).
  - **Redeem** (`RewardRedemptionService`, `POST {id}/Redeem` → `RewardRedemptionResultDto` with a `WalletChangeDto`): `RewardRedemptionRepository.RedeemAsync` takes `PlayerWallet.LockAsync` first and the service's callback checks the balance **under the lock** (`Rewards.InsufficientCoins` 409, nothing written), then stores the `RewardRedemption` (a copy of the name, icon and `CostPaid`, `RedeemedOn` = `AppClock.Today()`) and a `RewardRedeemed` ledger entry (`GameLedgerEntry.RewardId`, FK `SET NULL`). An archived reward can't be redeemed (`Rewards.Archived`).
  - **Undo** (`DELETE Redemptions/{redemptionId}`): only on `RedeemedOn` (`UndoOutsideWindow`), once (`AlreadyUndone`, re-checked under the lock with `UPDATE ... WHERE UndoneAt IS NULL`); refunds `CostPaid` with an `Undo` entry carrying the `RewardId`. `GET Redemptions` is paged newest first; each item has `CanUndo` (decided on the backend's today).
  - **`GET EarningPace`**: average coins per day earned with habits over the last `RewardPace.WindowDays` (14), from the player's first earning day when that is inside the window (`IGameLedgerRepository.GetHabitEarningsAsync`: `GameLedgerEntry.HabitEarningKinds` plus `Undo` entries with a `HabitId`; redemptions and knockouts don't count). The frontend shows it as "≈ N days of habits".

### StatementImports

- **Flow:** the frontend parses the bank file (OFX/CSV) and sends its lines; `POST api/StatementImports` stores a **draft** (`StatementImport` + one `StatementImportRow` per line, `Draft` status), already filed: categories suggested and duplicates left out. The user reviews the rows (`StatementImportRowService`), then `POST {id}/Commit` posts the included rows as transactions. Endpoints (`StatementImportsController`): `GET` (paged, drafts first then newest), `GET {id}` (the draft's months: counts, totals, existing month id; empty once committed), `POST`, `POST {id}/Commit`, `DELETE {id}` (discard a draft, or remove a committed import from the history), `GET {id}/Rows` (`month=yyyy-MM`, `filter=All|Uncategorized|Suggested|Duplicates|Excluded`, `search` over the description and merchant key, `sortBy=Date|Description|Category|Amount`), `PUT {id}/Rows/{rowId}`, `POST {id}/Rows/Bulk` (≤ 100 ids; only the given type/category/included change), `POST {id}/Rows/{rowId}/ApplyToSimilar`.
- **Rows** (`Domain/StatementImports/StatementImportRow.cs`): a transaction's shape (`TransactionDescription`, `TransactionAmount`, type, optional category) outside any month, so the date (2000-01-01..today, `DateOutOfRange`) can move anywhere. Negative amounts start as `Expense`, positive as `Income`; the description is cut to 80 (the bank's text, `OriginalDescription`, up to 255). `MerchantKey` (`Domain/Categorization/MerchantKey.cs`: normalized, digits/dates/card masks/1-letter words/company suffixes dropped, leading payment words like "compra cartao", "pix enviado" stripped, first 3 words) never changes. `ExternalId` = SHA-256 of the frontend's `accountKey` + the OFX `FITID` (`ImportExternalId`); a repeated one in the file is kept once. `EditedByUser` is set by `Edit`/`EditInBulk`; `CopyDecisionFrom` ("apply to similar") skips such rows, copies type/category/included (a duplicate stays out) and clears the suggestion. The `PUT` result carries `SimilarCount` (unedited rows of the same key filed differently) for the frontend's prompt.
- **Suggestions** (`Domain/Categorization/CategorySuggester.cs`, pure; built once per draft): evidence = the user's `CategorizationRule`s (learned on commit, weight 2) + their transactions grouped by normalized description/type/category (`GetCategoryHistoryAsync`, read as merchant keys, weight 1), each scaled by recency (`1 / (1 + months/12)`). Same key first: the best-scoring decision wins, `High` with ≥ 2 uses and ≥ 80% of the score, else `Medium`; source `Rule` or `History`; an `Ignore` rule leaves the row out. Otherwise keys with `TrigramSimilarity` ≥ 0.5 (C# copy of pg_trgm's `similarity`, compared in memory: rules are one row per merchant and decision), weighted by similarity, never `Ignore`, `Medium` only with ≥ 80% and similarity ≥ 0.7, else `Low`. Only decisions in the line's direction count (out = expense/investment, in = income).
- **Detections** (pure, in `Domain/StatementImports/`; they work on the first import, with no history): `InvestmentDetector.Detect(description, signedAmount)` → `InvestmentMovement` (`Application`/`Redemption`) from the normalized description's **leading words** (`aplicacao`, `aplic`, `dinheiro guardado`, `compra/debito tesouro direto`; `resgate`, `dinheiro resgatado`, `credito/venda tesouro direto`, `estorno aplicacao`), whole words only, and the direction decides (an application coming in is a refund, so a redemption). `OwnTransferDetector.For(User.Name)` (`IUserRepository.GetByIdAsync`, once per import) → `IsOwnTransfer`: the line starts with `pix`/`transferencia`/`transf`/`ted`/`doc`/`tev` and has every holder word (> 2 letters, name particles dropped, on both sides) in order, whole words: with `MinWordsForGaps` (3) words or more other names may come in between, a 2-word holder must appear together; a holder with fewer than `MinNameWords` (2) detects nothing. `StatementImportRow.ApplyInvestmentDetection` (application → `Investment`, included, optional category; redemption → left out) and `ApplyOwnTransferDetection` (left out), with `SuggestionSource` `InvestmentApplication`/`InvestmentRedemption`/`OwnTransfer` (5–7; the column is an integer, and they count as "suggested"), `High`, `Uses` 0. **Precedence** (`ImportRowFiler.File`, pure, called by the service per row): a `Rule`/`History` suggestion (same merchant key) → investment detection (an application keeps a `Similar` suggestion's category when it's an investment) → own transfer → `Similar` suggestion → the sign. Learning is unchanged: a detected line left out teaches `Ignore`, a confirmed application `Investment`.
- **Next-month shift** (a salary paid on the last business day of August is September's): `StatementImportRow.ShiftToNextMonth` keeps the bank's `Date` and sets the persisted, derived `EffectiveDate` to the 1st of the next month (`SetShift`, called by every path that changes `Date` or the shift; `FromPersistence` recomputes it). `Month` comes from `EffectiveDate`, so `GetMonthsAsync`, the rows' `month=` filter (both on the `EffectiveDate` column in SQL) and `CommitAsync`/`Transaction.CreateFromImport` all use it. Any type can be shifted. The row `PUT` sends `ShiftToNextMonth` explicitly (left out = false; the frontend unticks it when the date changes), the bulk edit takes `bool? ShiftToNextMonth`, `CopyDecisionFrom` copies it and `FiledDifferently` counts it. `ShiftLearned` marks a shift applied from history; any user edit clears it.
  - **Learning:** `RuleLearning.From` also emits, per included row in `StatementImportRow.IsInShiftWindow(Date)` (the last `ShiftWindowDays` = 5 days of its month), a `RuleAction.NextMonth` or `SameMonth` hit for (merchant key, final type), stored as `CategorizationRule`s with a null category (same table, index and upsert). `CategorySuggester` only reads `Categorize`/`Ignore` evidence.
  - **Applying it:** `MonthShiftAdvisor` (pure) sums `NextMonth` minus `SameMonth` hits per (key, type), weighted by `CategorySuggester.Recency`; `CreateAsync` calls `ApplyLearnedShift()` after `ImportRowFiler.File` (the type is final by then) on included rows in the window when the sum is positive.
- **Duplicates** (`DuplicateDetector`, pure, at creation over `GetForDuplicateCheckAsync`: the user's transactions dated from the statement's first date − 3 days to its latest `EffectiveDate` + 3 days, or with one of its external ids): same `ExternalId`; else same date and `SignedAmount` (closest merchant first); else same amount ≤ 3 days apart with similarity ≥ 0.5, all dates being the rows' `EffectiveDate` (a shifted salary matches the recurrence posted on the 1st). Each transaction matches one row at most. A duplicate row is excluded and keeps the transaction and month ids (not foreign keys) for the frontend's link. Duplicates are only looked for at creation, so a shift set by hand during review doesn't re-check them.
- **Commit** (`StatementImportService.CommitAsync`): included rows must all have a category (`UncategorizedRows`) of the user (`Category.NotFound`), at least one (`NothingToImport`). Missing months are opened with `MonthlySummary.OpenForImport` + `AddIfMissingAsync` (any year, like recurring postings), then `Transaction.CreateFromImport`. `StatementImportRepository.CommitAsync`, in one database transaction: `SELECT … FOR UPDATE` on the import `WHERE Status = Draft` (none → null → `AlreadyCommitted`, so a double confirmation posts once); per month in id order (no deadlock between imports), `MonthlySummaryTotals.LockAsync`, skip rows whose `ExternalId` is already in that month (`SkippedCount`), insert, `RecalculateAsync` once; upsert the `RuleLearning`s (`INSERT … SELECT FROM unnest(arrays) ON CONFLICT … DO UPDATE SET Hits = Hits + EXCLUDED.Hits`, unique `(UserId, MerchantKey, Type, Action, CategoryId)` with `NULLS NOT DISTINCT`); delete the rows; store `Committed`, `CommittedAt`, `ImportedCount`, `CreatedMonthCount`.
- **Learning** (`RuleLearning.From`): each included row is one hit for its (key, final type, category); each excluded non-duplicate row one hit for (key, type, `Ignore`); plus the month hits above. Rules are only written by commits (drafts and discards teach nothing). FK to the category `CASCADE`. Transactions typed by hand need no rule: they are already history.
- **Foreign keys:** rows → import `CASCADE`, rows → category `SET NULL` (deleting a category un-files draft rows; it doesn't block the deletion), imports and rules → user `CASCADE`.
- **Cleanup:** `StatementImportsCleanupRunner` (hourly `StatementImportsCleanupJob` locally, `--run-jobs` in production) deletes drafts older than `StatementImport.DraftRetentionDays` (30).
- **Tests:** domain tests for the key, similarity, suggester, duplicate/investment/own-transfer detectors, the filing precedence (`ImportRowFilerTests`), rows and learning; `StatementImportServiceTest` (including `RealStatementSample`, every investment and own-transfer line of three real Inter/Nubank statements, anonymized) and `StatementImportRowServiceTest` over `StatementImportRepositoryMock` (mirrors the commit: lock check through `BeforeNextCommit`, skipped external ids, totals via `TransactionRepositoryMock.RecalculateTotals`, rule upsert) and `CategorizationRuleRepositoryMock`. The repository's queries (`GetMonthsAsync` grouping, the left join with categories, the `unnest` upsert) are only verified against a real database.

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
  - **Time:** `TimeProvider` is replaced by the hand-written `Configurations/FakeTimeProvider` (starts at the real time). Cast it from the container and call `SetToday(date)` (noon UTC, the same date in São Paulo) to test date rules.
  - **Concurrency hooks:** `RecurringTransactionRepositoryMock.BeforeNextPost`/`BeforeNextUpdate` run right before the next cursor check, to simulate a concurrent posting or edit.
  - **E-mails:** `EmailSenderMock` keeps what was sent in the static `Sent` list (`Reset()` in the constructor; `FailNextSends` simulates a failure). Tests read the link's token from the body with a regex, and links point to `InjectionModule.TestFrontendBaseUrl`.
- `LifeManager.Domain.Test` tests value objects and entities directly with no DI, asserting on `Result.IsSuccess`/`Result.Error` and value-object `.Value` properties. Shared building blocks (`SearchText`, `PageRequest`/`PagedList`) are tested in `Shared/`.
