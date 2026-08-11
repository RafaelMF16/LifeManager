# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

LifeManager is a personal finance / life management API (.NET 10, C#). It is early-stage: the domain model (users, auth, categories, transactions, monthly summaries) and application services are built out, but `LifeManager.Infrastructure` (persistence) is still an empty project and `LifeManager.WebApi` has no controllers or DI wiring yet — `Program.cs` is the default ASP.NET Core template.

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

Note: `LifeManager.Tests` (singular) is a leftover scaffold project — it is not referenced in `LifeManager.slnx` and contains no code beyond its `.csproj`. The real test suites are `LifeManager.Domain.Test` and `LifeManager.Application.Test`.

## Architecture

Layered/Clean Architecture split across four projects, referencing inward only:

- **LifeManager.Domain** — entities, value objects, domain errors, repository interfaces (no external dependencies). Organized by bounded context/feature folder (`Users`, `Auth`, `Categories`, `Transactions`, `MonthlySummaries`), each with `ValueObjects/`, `Errors/`, and (where relevant) `Interfaces/` subfolders.
- **LifeManager.Application** — application services that orchestrate domain logic (`AuthService`, `TokenService`, `UserService`, `EnvironmentVariableService`) plus DTOs. Depends on `LifeManager.Domain` only.
- **LifeManager.Infrastructure** — intended home for persistence/repository implementations. Currently empty; nothing is implemented here yet.
- **LifeManager.WebApi** — ASP.NET Core host (`Microsoft.NET.Sdk.Web`). `Program.cs` currently only calls `AddControllers`/`AddOpenApi`; no application/domain services are registered in DI yet and the `Controllers/` folder is empty.

Test projects mirror the layer they test 1:1 (`LifeManager.Domain.Test` → Domain, `LifeManager.Application.Test` → Application) and reference only that layer (plus Domain, transitively).

### Domain-Driven Design

`LifeManager.Domain` is modeled with DDD tactical patterns, and the folder layout is the ubiquitous language:

- **Feature folders as bounded contexts** — `Users`, `Auth`, `Categories`, `Transactions`, `MonthlySummaries` each own their entity, value objects, errors, and repository interface. Cross-context references go through IDs (e.g. `Category.UserId`, `Transaction.MonthlySummaryId`), not object references, keeping contexts loosely coupled.
- **Entities** (`User`, `RefreshToken`, `Category`, `Transaction`, `MonthlySummary`) have identity (`Id`) and encapsulate their own invariants: private constructors force construction through a validating `static Create(...)` factory, and mutation happens only through intention-revealing methods (`AssignId`, `RevokeToken`) rather than public setters.
- **Value objects** (`Email`, `UserName`, `PasswordHash`, `PlainPassword`, `CategoryName`, `TransactionAmount`, `RefreshTokenHash`, id types like `UserId`/`CategoryId`, etc.) are immutable, validate themselves in `Create`, and implement structural `Equals`/`GetHashCode` — they are the primitives that make illegal states unrepresentable instead of passing raw strings/decimals around.
- **`Transaction` as the aggregate-root-like invariant enforcer** — it validates cross-entity consistency at creation time (e.g. `Transaction.Create` rejects a `MoneyFlowType` that doesn't match its `Category.Type`), which is where a genuine business rule (not just field validation) lives.
- **Domain errors as part of the model** — each context's `*Errors` static class (`UserErrors`, `AuthErrors`, ...) enumerates the domain's known failure modes by name (e.g. `UserErrors.EmailRegistered`, `UserErrors.InvalidCredentials`), so failure cases are discoverable and testable like any other part of the ubiquitous language, not ad-hoc exception messages.
- Repository interfaces (`IUserRepository`, `IRefreshTokenRepository`) live in the Domain layer, expressed in domain terms, with implementations deferred to `LifeManager.Infrastructure` (not yet written) — a standard DDD/hexagonal port-adapter split.

### Result pattern (no exceptions for expected failures)

The codebase is mid-migration to a `Result`/`Result<T>` pattern (`LifeManager.Domain/Shared/Results/`) for anything that can fail validation, mirroring a typical Railway-Oriented-Programming style:

- `Error` is a record with a `Code`, `Message`, and `ErrorType` (`Validation`, `NotFound`, `Unauthorized`, `Failure`, `Conflict`), created via static factories (`Error.Validation(...)`, `Error.Conflict(...)`, etc.).
- `Result` / `Result<T>` have implicit conversions from `Error` and from `T`, so factory methods can `return SomeErrors.Whatever;` or `return new Thing(...)` directly instead of throwing.
- `ResultExtensions` provides `Map`, `Bind`, and `Tap` for chaining `Result<T>` operations functionally (see `User.Create` and `TokenService.GenerateTokens`/`SaveRefreshToken` for the chaining style).
- `Users` and `Auth` (`User`, `Email`, `PasswordHash`, `PlainPassword`, `UserName`, `RefreshToken`, `RefreshTokenHash`) have been migrated to this pattern.
- `Categories`, `Transactions`, and `MonthlySummaries` have **not** been migrated yet — their `Create` methods return the entity/value object directly and use `DomainException` for invariant violations (see `Transaction.Create` throwing `DomainException` when the money-flow type mismatches). When touching these areas, check with the user whether to migrate them to `Result` first, since this is an active, incremental refactor (see recent commit history: "Implementando result pattern no auth service", "Fluxo de user com result pattern implementado").

### Application services and DI

Services use primary-constructor dependency injection (e.g. `TokenService(IRefreshTokenRepository refreshTokenRepository, EnvironmentVariableService environmentVariableService)`) against Domain repository interfaces — there is no concrete repository implementation yet outside of test mocks.

`TokenService` reads JWT signing secrets via `EnvironmentVariableService` (backed by `IConfiguration`) using the keys `accessTokenSecretKey` and `refreshTokenSecretKey`; it throws if a key is missing. Access tokens expire in 15 minutes, refresh tokens in 7 days; refresh tokens are stored hashed (HMAC-SHA256) and any previously active token for a user is revoked when a new one is issued.

## Testing conventions

- xUnit, `Assert.*` style, test names follow `Method_ShouldExpectedBehavior_WhenCondition`.
- `LifeManager.Application.Test` builds its own mini DI container per test class via `BaseTest` (in `Configurations/`), which registers real application services against in-memory repository mocks (`Configurations`/`*/Mocks`) and an in-memory `IConfiguration` with test secret keys. Test classes extend `BaseTest` and resolve services with `ServiceProvider.GetRequiredService<T>()`; they're tagged `[Collection("ApplicationServices")]` since the mocks back onto shared singleton in-memory lists (`Configurations/SingletonLists/UserSingleton.cs`, `RefreshTokenSingleton.cs`) that persist across tests in the same run — be aware of cross-test data when adding new tests against these mocks.
- `LifeManager.Domain.Test` tests value objects and entities directly with no DI, asserting on `Result.IsSuccess`/`Result.Error` and value-object `.Value` properties.
