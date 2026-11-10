# Spec: `home-overview` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (M3 + V5). Branch `feature/home-overview`, a partir da `dev`, nos dois repos. Tela: [`LifeManagerFront/docs/specs/home-overview.md`](../../../LifeManagerFront/docs/specs/home-overview.md).

## Objetivo

A Home é a primeira tela depois do login e hoje só mostra dois cartões. Ela passa a responder, com uma única chamada, "como está o meu dia": hábitos que faltam, saldo do mês e o orçamento mais apertado.

## Contrato: `GET /api/Home/Overview`

```jsonc
{
  "today": "2026-11-09",                       // AppClock.Today()
  "habits": {
    "dueToday": 4, "doneToday": 1,              // hábitos a construir com agenda hoje (as mesmas regras do GET Habits/Today)
    "yesterdayPending": 1,                      // ainda dá para marcar ontem
    "avoiding": 1, "relapsedToday": 0,
    "bestStreak": { "habitId": 7, "name": "Ler 10 páginas", "days": 12 } // null sem sequência
  },
  "player": { "level": 3, "hp": 80, "maxHp": 100, "coins": 240 },          // null antes do primeiro uso do módulo
  "finance": {
    "month": "2026-11", "monthlySummaryId": 47,  // null se o mês corrente não existe
    "income": 8000.00, "expense": 3120.50, "investment": 1000.00, "balance": 3879.50,
    "budget": {                                  // null sem plano no mês
      "spendingLimit": 4510.00, "spent": 3120.50,
      "tightest": { "categoryId": 3, "name": "Mercado", "ratio": 0.92 }  // a fatia com maior gasto/meta; null sem fatias
    }
  }
}
```

## Regras

- **Leitura pura:** o endpoint não chama `AchievementService.SyncAsync` nem `ChallengeService.SyncAsync`, porque a Home não é lugar de efeito colateral. A sincronização continua nas telas do módulo Hábitos.
- **Hábitos:** usam as mesmas regras de agenda do `GET Habits/Today` (`Habit.IsDueOn`, o check-in de hoje e de ontem), extraídas para um método compartilhado. Assim os números da Home nunca divergem da tela Hoje.
  - O plano decide se é um método de `HabitTodayService` sem a sincronização, ou uma consulta própria com as mesmas regras puras.
  - O critério é **um teste que compara os dois** para o mesmo cenário.
- **Finanças:**
  - O mês é o corrente no `AppClock` (`IMonthlySummaryRepository.GetByPeriodAsync`).
  - O orçamento vem do mesmo cálculo da tela Orçamento (`BudgetPlanService.GetAsync` ou a mesma agregação).
  - `tightest` é a fatia de despesa com a maior razão gasto/valor, sem contar a parte livre.
- **Desempenho:** no máximo 6 consultas, sequenciais no mesmo `DbContext` (nunca em paralelo), todas `AsNoTracking`. Nenhuma lista paginada.
- **Serviço novo:** `HomeOverviewService` em `Application/Home/`, com DTOs em `Application/Home/DTOs/`. Controller `WebApi/Home/Controllers/HomeController.cs` com `[Authorize]`.

## Testes (Application)

- Sem hábitos e sem mês: `habits` zerado, `player` e `finance.monthlySummaryId` nulos.
- Com hábitos: `dueToday`/`doneToday` iguais ao `GET Habits/Today` do mesmo cenário (o teste de paridade).
- Um hábito com agenda só em outro dia da semana não entra em `dueToday`.
- Orçamento: `tightest` aponta a fatia certa e é `null` sem plano.
- Isolamento: os dados de outro usuário nunca entram.
- O endpoint não grava nada (os contadores de escrita dos mocks ficam em zero).

## Docs

- `CLAUDE.md`: uma seção "Home overview" curta.
- `docs/regras-de-negocio.md`: nada muda, porque o endpoint só lê regras existentes.

## Arquivos

`LifeManager.Application/Home/` (serviço + DTOs), `LifeManager.WebApi/Home/Controllers/HomeController.cs`, `DI/DependencyInjection.cs`, os testes e o `CLAUDE.md`. **Sem migration.**
