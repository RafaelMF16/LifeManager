# Spec: `savings-goals` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (N4). Branch `feature/savings-goals`, a partir da `dev`, nos dois repos. Tela: [`LifeManagerFront/docs/specs/savings-goals.md`](../../../LifeManagerFront/docs/specs/savings-goals.md).

## Objetivo

O orçamento cobre o mês. Nada cobre um objetivo de médio prazo, como "Viagem: R$ 5.000 até dez/2027" ou "Reserva de emergência: R$ 20.000".

Um objetivo de economia tem uma meta e, opcionalmente, um prazo. Ele cresce com os lançamentos de **investimento** ligados a ele, o tipo que já representa "dinheiro guardado". Assim o usuário vê quanto falta e quanto precisa guardar por mês.

## Modelo

**`SavingsGoal`** (`Domain/SavingsGoals/`):

| Campo | Regras |
|---|---|
| `Name` | 1–60, normalizado (`NormalizedName`, busca e unicidade entre os ativos) |
| `TargetAmount` | regras de `TransactionAmount` (> 0, 2 casas) |
| `TargetMonth` | `YearMonth` opcional, **não pode estar no passado** (vale na criação e na edição) |
| `InitialAmount` | ≥ 0, o que já estava guardado antes |
| `Icon` | um id que o front conhece (≤ 30), como as recompensas |
| `ArchivedAt` | apagar **arquiva**, como hábitos e recompensas |
| `CompletedAt` | data em que a meta foi atingida pela primeira vez; informativo, nunca volta |

**Progresso:** `Saved = InitialAmount + Σ Amount` dos lançamentos `Investment` com `SavingsGoalId = goal.Id`. É calculado na leitura com um `GROUP BY` e não é guardado, para nunca divergir dos lançamentos.

**Derivados** (puros, `SavingsGoalProgress`): `Ratio`, `Remaining`, `MonthsLeft` (do mês corrente no `AppClock` até `TargetMonth`, inclusive) e `MonthlyNeeded = Remaining / MonthsLeft` (arredondado para cima em centavos). Também `Pace`:
- `OnTrack`: a média guardada nos últimos 3 meses cobre o `MonthlyNeeded`;
- `Behind`: não cobre;
- `NoDeadline`: sem prazo;
- `Done`: meta atingida.

## Ligação com os lançamentos

- **`Transactions.SavingsGoalId`** (nulo, FK `SET NULL`): apagar ou arquivar o objetivo nunca apaga lançamentos.
  - Só vale para lançamentos `Investment` (`Transaction.GoalRequiresInvestment`).
  - O objetivo precisa ser do usuário e estar ativo (`SavingsGoal.NotFound`).
- Os bodies de create/update do lançamento (aninhado e o `POST /api/Transactions` do `quick-transaction`) e o `TransactionResponseDto` ganham `savingsGoalId?`. A listagem ganha o filtro `savingsGoalId=`.
- **Retirar dinheiro do objetivo:** fora deste módulo (ver Decisões). Por enquanto, o usuário edita ou apaga o lançamento de investimento.

## Contrato: `api/SavingsGoals`

- `GET`: listagem paginada (`status=Active|Archived|Completed`, `search`, `sortBy=Name|TargetMonth|Progress`).
- `GET {id}`: o objetivo, o progresso e `monthly` (o guardado por mês dos últimos 12 meses, para o gráfico).
- `POST` → 201, `PUT {id}`, `DELETE {id}` (arquiva), `POST {id}/Restore`.
- Códigos novos: `SavingsGoal.NameAlreadyExists` (409), `SavingsGoal.TargetMonthInPast`, `SavingsGoal.Archived` (409, ao ligar um lançamento), `Transaction.GoalRequiresInvestment`.

## Migration (aditiva): `AddSavingsGoals`

- **Tabela `SavingsGoals`:** FK do usuário `CASCADE`; índice único parcial `(UserId, NormalizedName) WHERE ArchivedAt IS NULL`; índice GIN trigram no `NormalizedName`.
- **Coluna `Transactions.SavingsGoalId`:** com índice.

## Fora do escopo

Vincular objetivos na importação de extrato e conquistas de objetivos. Ficam para depois, se fizer sentido.

## Testes

- **Domain:** as regras do VO e do prazo, e `SavingsGoalProgress` (meta atingida, prazo neste mês, sem prazo, `Pace`).
- **Application:**
  - CRUD com nome repetido, arquivar e restaurar;
  - ligar lançamento: só `Investment`, objetivo de outro usuário, objetivo arquivado;
  - o progresso soma só os lançamentos ligados;
  - apagar o lançamento reduz o progresso;
  - `CompletedAt` é gravado uma vez.
- **Integração** (se o `integration-tests` já estiver na `dev`): o `GROUP BY` do progresso e o `SET NULL` ao apagar.

## Docs

`CLAUDE.md` (seção SavingsGoals) e `docs/regras-de-negocio.md` (regras e códigos).

## Arquivos

`Domain/SavingsGoals/`, `Domain/Transactions/` (campo + regra), `Application/SavingsGoals/`, `Application/Transactions/` (DTOs), `Infrastructure/SavingsGoals/`, `Infrastructure/Transactions/` (filtro), `Infrastructure/Migrations/`, `WebApi/SavingsGoals/Controllers/`, os mocks e os testes.

## Decisões

- **Retirada** (09/11/2026): usar o dinheiro, como pagar a viagem, **não** baixa o objetivo neste módulo. O objetivo mostra o quanto foi guardado e pode ser "concluído". A retirada pode virar um tipo de movimento depois.
- **Plano aprovado** (11/11/2026), o que o spec não fixava:
  - **Lançamentos do objetivo no detalhe:** `GET api/SavingsGoals/{id}/Transactions`, paginado, de todos os meses, do mais novo para o mais antigo, cada item com `monthlySummaryId`. O filtro `savingsGoalId=` da listagem do mês também existe.
  - **Objetivo arquivado:** o 409 só vale quando o `savingsGoalId` do lançamento **muda**. Editar outro campo mantendo o objetivo arquivado passa.
  - **Ritmo:** os "últimos 3 meses" são o corrente e os 2 anteriores, sempre divididos por 3.
  - **`status=Completed`:** objetivo ativo com `CompletedAt`. Continua lá mesmo se o guardado cair depois; só o `pace` sai de `Done`.
  - **`sortBy=TargetMonth`:** sem prazo por último na ordem crescente.
  - **Códigos a mais:** `SavingsGoal.NameIsNullOrWhiteSpace`, `NameTooLong`, `InvalidTargetAmount`, `InvalidInitialAmount`, `InvalidTargetMonth`, `IconTooLong`, `AlreadyArchived`, `NotArchived` (todos em `docs/regras-de-negocio.md`).
  - **Resposta** (listagem, detalhe, create/update/restore): os campos do objetivo + `saved`, `ratio`, `remaining`, `monthsLeft`, `monthlyNeeded`, `pace` e `completedAt`; `monthly` (`[{ month: "yyyy-MM", amount }]`, 12 itens) só no `GET {id}`.
