# Spec: `quick-transaction` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (M1). Branch `feature/quick-transaction`, a partir da `dev`, nos dois repos, **depois** do `review-fixes`. Tela: [`LifeManagerFront/docs/specs/quick-transaction.md`](../../../LifeManagerFront/docs/specs/quick-transaction.md).

## Objetivo

Lançar um gasto não pode exigir criar o mês antes. Hoje são três passos (criar o mês, entrar nele, lançar) e, em janeiro, não dá para criar dezembro à mão.

- **Usuário:** quem anota um gasto no celular, na hora.
- **Sucesso:** um lançamento novo é um formulário só, e o mês aparece sozinho.

## Escopo

### 1. Lançamento sem mês: `POST /api/Transactions`

Body igual ao do lançamento aninhado: `{ type, description, amount, date, categoryId }`. Resposta **201** com o `TransactionResponseDto` de sempre, mais `monthlySummaryId` e `monthCreated` (bool).

- **Mês existente:** o serviço procura o mês do usuário para `date.Year/Month` (`IMonthlySummaryRepository.GetByPeriodAsync`). Se existe, lança nele, seja qual for o ano: é o mesmo que lançar dentro do mês.
- **Mês que falta:** só abre meses do **ano corrente ou do anterior**, no fuso do negócio (`AppClock`). Fora disso, devolve `MonthlySummary.YearNotCurrent` (400, o código de sempre).
  - O mês é aberto com um factory novo, `MonthlySummary.OpenForEntry(userId, yearMonth, currentYear)`, e `AddIfMissingAsync` (`ON CONFLICT DO NOTHING`, à prova de dois lançamentos simultâneos).
  - Depois disso, o fluxo é o de `TransactionService.CreateAsync`: mesmas regras, mesmo recálculo de totais.
- **Data no futuro:** segue a regra atual do lançamento aninhado (só precisa caber no mês). Este módulo não muda isso.
- **Check-in automático:** chama `HabitAutoCheckInService.OnEventAsync(TransactionCreated)` como o create aninhado, e a resposta traz `autoCheckIns`.
- **Ordem das validações:** primeiro a categoria (`Category.NotFound`) e as regras do lançamento, **depois** a abertura do mês. Assim, um lançamento inválido nunca deixa um mês vazio criado.
- O controller novo é `TransactionsEntryController`, ou uma action no `TransactionsController` com rota absoluta `[HttpPost("/api/Transactions")]`. A decisão fica para o plano: o que importa é a rota.

### 2. Ano anterior na criação manual

- `POST /api/MonthlySummaries` passa a aceitar `{ month, year? }`. Sem `year`, o ano é o corrente (`AppClock`), como hoje.
- `MonthlySummaryYear.Create(value, currentYear)` (vindo do `review-fixes`) aceita `currentYear` **ou** `currentYear - 1`. O código de erro continua `MonthlySummary.YearNotCurrent`, porque os códigos não se renomeiam. Muda a mensagem: "O ano deve ser o corrente ou o anterior."
- `AlreadyExists` (409) continua igual.

### Observação: conquistas de Finanças (V6)

Abrir meses passados à mão pode liberar conquistas de Finanças na hora: os meses fechados contam. O risco já existe pela importação e é baixo (o ranking não usa moedas). A decisão continua em aberto no mapa e fica **fora** deste módulo.

## Testes

- **Domain:**
  - `MonthlySummaryYear` aceita o ano corrente e o anterior e recusa o de dois anos atrás e o seguinte;
  - `OpenForEntry` segue a mesma regra.
- **Application** (`TransactionEntryServiceTest`, ou junto do `TransactionServiceTest`):
  - lança num mês existente de 3 anos atrás (passa);
  - abre o mês que falta no ano corrente (`monthCreated: true`, totais certos);
  - abre dezembro do ano anterior em janeiro (`FakeTimeProvider`);
  - recusa um mês que falta de 2 anos atrás;
  - uma categoria de outro usuário não cria o mês;
  - dispara o check-in automático.
- `MonthlySummaryServiceTest`: `year` omitido usa o ano corrente; `year` do ano anterior passa; `year` de 2 anos atrás devolve `YearNotCurrent`.
- **Portão:** `dotnet build` com 0 warnings e `dotnet test` verde. Se o `integration-tests` já estiver na `dev`, um teste de `AddIfMissingAsync` concorrente.

## Docs

- `CLAUDE.md` (MonthlySummaries e Transactions): a rota nova e a regra do ano anterior.
- `docs/regras-de-negocio.md`: as regras novas de criação de mês e de lançamento rápido, e a mensagem do `YearNotCurrent`.

## Arquivos

`Domain/MonthlySummaries/` (VO + factory), `Application/Transactions/` (serviço + DTO), `Application/MonthlySummaries/` (DTO com `year?`), `WebApi/Transactions/Controllers/`, os testes, `CLAUDE.md` e `docs/regras-de-negocio.md`. **Sem migration.**
