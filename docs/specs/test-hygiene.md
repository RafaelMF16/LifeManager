# Spec: `test-hygiene`

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (P4 + P5). Branch `chore/test-hygiene`, a partir da `dev`.

## Objetivo

Um build sem ruído, para que um warning novo apareça.

## Escopo

1. **Apagar `LifeManager.Tests/`.** É só um `.csproj` de scaffold, fora do `.slnx`. A nota sobre ele sai do `CLAUDE.md`.
2. **Zerar os 61 `CS8602`** de `LifeManager.Application.Test`. Estão em `HabitStatsServiceTest`, `HabitServiceTest`, `RewardServiceTest`, `RewardRedemptionServiceTest` e `StatementImportServiceTest`. Eles vêm de `result.Value.X` sobre `Result<T>.Value` anulável.
   - A correção é afirmar antes de usar (`Assert.NotNull(result.Value)` ou `result.Value!` depois de `Assert.True(result.IsSuccess)`), seguindo o padrão que já existe nos testes.
   - **Nada de `#pragma`/`NoWarn`.**
3. **Travar o portão:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` nos dois projetos de teste. Fica só nos testes, para o build de produção não quebrar por um warning de pacote.

## Critérios de aceite

- `dotnet build LifeManager.slnx` → **0 Warning(s)**.
- `dotnet test LifeManager.slnx` → os mesmos 2.019 testes, todos verdes: nenhum teste removido nem pulado, nenhuma asserção enfraquecida.
- A pasta `LifeManager.Tests/` sai do repo.

## Arquivos

`LifeManager.Tests/` (removido), os 5 arquivos de teste acima, os `.csproj` de `LifeManager.Application.Test` e `LifeManager.Domain.Test`, e `CLAUDE.md`.
