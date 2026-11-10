# Specs do back

O mapa completo (diagnóstico, ordem, decisões e as specs dos dois repos) fica em [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md). Aqui ficam só os specs com trabalho no back.

| Spec | Branch | Status |
|---|---|---|
| `review-fixes` (relógio, higiene dos testes e CI; o spec ficava no front e já saiu) | `fix/review-fixes` | **mergeado na `dev`** em 10/11/2026 (PR #41) |
| `integration-tests` (Testcontainers, um teste por repositório e a migration `MakeUserDeletionForeignKeysDeferrable`; o spec já saiu) | `test/integration-tests` | **mergeado na `dev`** em 10/11/2026 (PR #43) |
| `quick-transaction` (`POST /api/Transactions` e o ano anterior na criação de mês; o spec já saiu) | `feature/quick-transaction` | **mergeado na `dev`** em 10/11/2026 (PR #45) |
| `home-overview` (`GET /api/Home/Overview`, a `HabitDayAgenda` compartilhada com a tela Hoje e o plano com o gasto das fatias; o spec já saiu) | `feature/home-overview` | **mergeado na `dev`** em 10/11/2026 (PR #47) |
| [`account-settings`](account-settings.md) | `feature/account-settings` | **implementado** na branch (10/11/2026), falta o roteiro manual e o PR para a `dev` |
| [`habit-reminders`](habit-reminders.md) | `feature/habit-reminders` | aprovado · dependência em discussão |
| [`savings-goals`](savings-goals.md) | `feature/savings-goals` | aprovado |
| [`transactions-export`](transactions-export.md) | `feature/transactions-export` | aprovado |

## Regras

- Um módulo que mexe nos dois repos tem um spec **em cada um**, com o mesmo id e a mesma branch:
  - o spec do back traz o **contrato da API**;
  - o do front traz a tela e aponta para ele.
- **Spec implementado e mergeado na `dev` sai daqui.** O histórico fica no git e as regras vão para o `CLAUDE.md` e para `docs/regras-de-negocio.md`.
