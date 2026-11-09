# Spec: `integration-tests`

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (P3). Branch `test/integration-tests`, a partir da `dev`, **depois** do `review-fixes`.

## Objetivo

Hoje nada roda contra um Postgres de verdade: as migrations, os mapeamentos EF e o SQL cru só são verificados quando a API sobe. Este módulo cria uma camada de testes contra um Postgres real, descartável, que roda no `dotnet test`, local e no CI. Ela não substitui os mocks.

**Sucesso:** um erro de SQL, de mapeamento ou de migration quebra o `dotnet test` antes do deploy, e não em produção.

## Escopo

1. **Projeto novo `LifeManager.Infrastructure.Test`** (xUnit), no `.slnx`, referenciando só `LifeManager.Infrastructure`, com `TreatWarningsAsErrors` igual aos outros.
2. **Postgres descartável** com o `Testcontainers.PostgreSql` (dependência nova, **em discussão**), na mesma imagem do `docker-compose.yml` (`postgres:16-alpine`).
   - Uma `PostgresFixture` (`ICollectionFixture`) sobe **um** contêiner por execução e aplica `MigrateAsync()`. Isso já testa toda a cadeia de migrations e falha se houver mudança de modelo sem migration.
   - Cada teste roda num banco limpo: `TRUNCATE … RESTART IDENTITY CASCADE` das tabelas do usuário antes do teste.
   - Testes marcados com `[Trait("Category", "Integration")]`, para poder rodá-los à parte (`--filter Category=Integration`).
3. **Builders de dados** (`Seed/`): usuário, categorias, mês e lançamentos inseridos pelo próprio `DbContext`, com os factories do domínio.
4. **Cobertura inicial**, focada no que o mock não consegue provar:

| Área | O que o teste garante |
|---|---|
| Migrations | `MigrateAsync` num banco vazio chega à última migration, e `GetPendingMigrations` fica vazio |
| `TransactionRepository` | create/update/delete recalculam `TotalIncome`/`TotalExpense`/`TotalInvestment`/`BalanceAmount`; 10 escritas concorrentes no mesmo mês (`Task.WhenAll`, um `DbContext` por tarefa) terminam com o total certo (`FOR UPDATE`) |
| Busca | `Categories` e `Transactions` acham "mercadó" com "MERCADO" (coluna normalizada + `LIKE` com escape de `%` e `_`) |
| `MonthlySummaryRepository` | `GetNeighborsAsync` atravessa a virada de ano; `AddIfMissingAsync` não duplica (`ON CONFLICT DO NOTHING`) |
| `RecurringTransactionRepository` | o CAS do cursor: duas postagens concorrentes do mesmo mês geram um lançamento só |
| `StatementImportRepository` | `CommitAsync`: pula `ExternalId` repetido, abre meses que faltam, o upsert de regras com `unnest` soma `Hits`, e uma segunda confirmação devolve `AlreadyCommitted` |
| `PlayerWallet` | duas check-ins concorrentes do mesmo usuário serializam (o saldo final é a soma das duas) |
| Auth | `EmailCooldownRepository.TryReserveAsync` e o upsert do `LoginAttempt` (janela de 15 min) |
| `FinanceDashboardRepository` | o agrupamento por mês/categoria bate com a soma dos lançamentos |
| Exclusão de usuário | `DELETE` do usuário apaga em cascata tudo o que é dele (base do `account-settings`) |

Fora do escopo: testes HTTP de ponta a ponta (`WebApplicationFactory`), que podem vir num módulo próprio.

## CI

- O `deploy.yml` já roda `dotnet test LifeManager.slnx` num runner `ubuntu-latest`, que tem Docker. Nada muda no workflow, só o tempo do job: a meta é **≤ 2 min** a mais.
- **Local:** precisa do Docker rodando (o mesmo do `docker-compose`). Sem Docker, os testes de integração **falham** com uma mensagem clara: nada de pular em silêncio.

## Critérios de aceite

- `dotnet test LifeManager.slnx` roda os três projetos, todos verdes, com 0 warnings.
- Quebrar de propósito uma query (por exemplo, trocar `LIKE` por `Contains` na busca de categorias) derruba um teste. Isso é conferido uma vez, à mão, e descrito no PR.
- O `CLAUDE.md` deixa de dizer "nothing runs against a real Postgres" e ganha uma seção "Integration tests" (fixture, banco limpo, como rodar só eles). O `README.md` diz que o `dotnet test` precisa do Docker.

## Arquivos

`LifeManager.Infrastructure.Test/` (novo: `.csproj`, `PostgresFixture.cs`, `Seed/`, um arquivo de teste por repositório), `LifeManager.slnx`, `CLAUDE.md` e `README.md`.

## Perguntas em aberto

1. **Dependência nova (`Testcontainers.PostgreSql`):** em discussão. O módulo só começa depois dessa conversa.
