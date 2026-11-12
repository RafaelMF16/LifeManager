# Specs do back

O mapa completo (diagnóstico, ordem, decisões e as specs dos dois repos) fica em [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md). Aqui ficam só os specs com trabalho no back.

| Spec | Branch | Status |
|---|---|---|
| `review-fixes` (relógio, higiene dos testes e CI; o spec ficava no front e já saiu) | `fix/review-fixes` | **mergeado na `dev`** em 10/11/2026 (PR #41) |
| `integration-tests` (Testcontainers, um teste por repositório e a migration `MakeUserDeletionForeignKeysDeferrable`; o spec já saiu) | `test/integration-tests` | **mergeado na `dev`** em 10/11/2026 (PR #43) |
| `quick-transaction` (`POST /api/Transactions` e o ano anterior na criação de mês; o spec já saiu) | `feature/quick-transaction` | **mergeado na `dev`** em 10/11/2026 (PR #45) |
| `home-overview` (`GET /api/Home/Overview`, a `HabitDayAgenda` compartilhada com a tela Hoje e o plano com o gasto das fatias; o spec já saiu) | `feature/home-overview` | **mergeado na `dev`** em 10/11/2026 (PR #47) |
| `account-settings` (Minha conta: renomear, trocar senha, sair de todos, exportar em JSON e exclusão com 7 dias de prazo e o job `AccountDeletionRunner`; o spec já saiu) | `feature/account-settings` | **mergeado na `dev`** em 10/11/2026 (PR #50) |
| `transactions-export` (`GET /api/Transactions/Export`: CSV de um período em streaming, PtBr/EnUs, origem de cada lançamento e rate limit `TransactionsExport`; o spec já saiu) | `feature/transactions-export` | **mergeado na `dev`** em 11/11/2026 (PR #53) |
| `savings-goals` (`api/SavingsGoals`: objetivos com progresso calculado na leitura, investimentos ligados por `Transactions.SavingsGoalId`, `CompletedAt` gravado uma vez e a migration `AddSavingsGoals`; o spec já saiu) | `feature/savings-goals` | **mergeado na `dev`** em 11/11/2026 (PR #55) |
| [`habit-reminders`](habit-reminders.md) | `feature/habit-reminders` | aprovado · dependência em discussão |

## Regras

- Um módulo que mexe nos dois repos tem um spec **em cada um**, com o mesmo id e a mesma branch:
  - o spec do back traz o **contrato da API**;
  - o do front traz a tela e aponta para ele.
- **Spec implementado e mergeado na `dev` sai daqui.** O histórico fica no git e as regras vão para o `CLAUDE.md` e para `docs/regras-de-negocio.md`.
