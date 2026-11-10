# Spec: `transactions-export` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (N5). Branch `feature/transactions-export`, a partir da `dev`, nos dois repos. Tela: [`LifeManagerFront/docs/specs/transactions-export.md`](../../../LifeManagerFront/docs/specs/transactions-export.md).

## Objetivo

Baixar os lançamentos de um período num CSV que abre direto no Excel ou no Google Planilhas, para análise própria, para o contador ou para levar a outro app. Isso complementa a importação: o dado que entra também sai.

É diferente da exportação da conta (`account-settings`), que é um JSON completo para a LGPD. Aqui é uma planilha de lançamentos.

## Contrato: `GET /api/Transactions/Export?from=yyyy-MM&to=yyyy-MM&type=All|Expense|Income|Investment&format=PtBr|EnUs`

- **Resposta:** 200 `text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="lancamentos-2026-01_2026-11.csv"`.
- **Período:** as mesmas regras do dashboard (`DashboardPeriod`/`YearMonth`): `to ≥ from`, no máximo **36 meses**. Os erros reaproveitam os códigos do dashboard (`FinanceDashboard.InvalidFrom`/`InvalidTo`/`EndBeforeStart`/`PeriodTooLong`), sem códigos novos.
- **Colunas:** `Data`, `Descrição`, `Categoria`, `Tipo`, `Valor`, `Mês`, `Origem` (`Manual`/`Recorrente`/`Importação`/`Resgate`).
  - Com `format=EnUs`, os cabeçalhos e os tipos saem em inglês.
  - `Valor` sai **com sinal**: receita positiva; despesa e investimento negativos. A soma da coluna dá o saldo do período.
- **Formato:**

| | `PtBr` (padrão) | `EnUs` |
|---|---|---|
| Separador | `;` | `,` |
| Decimal | vírgula | ponto |
| Data | `dd/MM/yyyy` | `yyyy-MM-dd` |

- O arquivo começa com **BOM UTF-8**, para o Excel no Windows ler os acentos.
- Campos com separador, aspas ou quebra de linha vão entre aspas (RFC 4180).
- **Segurança (CSV injection):** uma célula de texto que começa com `=`, `+`, `-`, `@`, tab ou CR ganha um `'` na frente. A descrição vem do usuário e do banco, então isso é obrigatório.
- **Ordem:** data, depois `Id`.
- **Streaming:** `AsAsyncEnumerable` com `AsNoTracking`, escrevendo linha a linha na resposta. Um período de 36 meses nunca é carregado inteiro na memória.
- **`X-Row-Count`:** antes do streaming, um `COUNT` com o mesmo filtro preenche esse cabeçalho. O front usa o número para avisar quando o período está vazio.
- **CORS (só no dev, onde front e API têm origens diferentes):** a política `AllowFrontend` já expõe o `Content-Disposition` (desde o `account-settings`) e ganha o `X-Row-Count` em `WithExposedHeaders`. Sem isso, o front não lê esse cabeçalho.
- **Rate limit:** política `TransactionsExport`, 10 por 10 min por usuário.

## Desenho

- `TransactionExportService` (Application) recebe a consulta validada e um `Stream`. O formatador de linhas é **puro** (`TransactionCsvFormatter`, no Domain ou na Application) e testável sem banco.
- O repositório devolve `IAsyncEnumerable<TransactionExportRow>`, com o filtro por usuário pelo mês, igual ao dashboard.

## Testes

- **Formatador:**
  - os dois formatos;
  - aspas e separador dentro da descrição;
  - CSV injection (`=SOMA(...)` sai como `'=SOMA(...)`);
  - BOM;
  - sinal por tipo;
  - origem.
- **Serviço:**
  - período inválido;
  - período longo demais;
  - filtro por tipo;
  - os lançamentos de outro usuário nunca saem;
  - a ordem.

## Docs

`CLAUDE.md` (Transactions: a rota de exportação) e `docs/regras-de-negocio.md` (o que o arquivo contém e o limite de período).

## Arquivos

`Application/Transactions/` (serviço, formatador, DTO da consulta), `Infrastructure/Transactions/` (consulta), `WebApi/Transactions/Controllers/`, o mock e os testes. **Sem migration.**
