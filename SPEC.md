# Spec: Detecção de investimentos e transferências entre contas próprias na importação de extrato

## Objetivo

Ao importar um extrato (OFX ou CSV), as linhas devem chegar à revisão já classificadas assim, mesmo na primeira
importação, quando o usuário ainda não tem histórico nem regras aprendidas:
- **aplicações** como **Investimento**;
- **resgates** **fora da importação**;
- **transferências entre contas próprias** (Pix/TED para o próprio nome em outro banco) **fora da importação**.

**Por que falha hoje**
- `StatementImportRow.Create` decide o tipo só pelo sinal do valor: saída vira `Expense` e entrada vira `Income`.
- Só o `CategorySuggester` muda isso (para `Investment` ou "deixar de fora"), e ele depende de decisões anteriores do
  usuário. Na primeira importação não existe nenhuma.
- Uma entrada nunca vira investimento (`InDirection`). Por isso resgates e transferências recebidas entram como
  **receita** e aumentam a renda do mês. As transferências enviadas entram como **gasto**.

**Evidência**: três arquivos de exemplo (Inter 2025, Nubank jan/2026 e Nubank set/2026). Os nomes estão trocados por `<TITULAR>`.

| Banco | Descrição como chega ao backend (`describe(NAME, MEMO)`) | Sinal | Qtde | Esperado |
|---|---|---|---|---|
| Inter | `Aplicacao: "CDB PORQUINHO BANCO INTER S A"` | − | 8 | Investimento |
| Inter | `Aplicacao: "CDB POS DI LIQ. BANCO INTER SA"` (e `S A`) | − | 10 | Investimento |
| Inter | `Resgate: "CDB POS DI LIQ. BANCO INTER S A"` | + | 13 | Fora (resgate) |
| Inter | `Resgate: "CDB PORQUINHO BANCO INTER S A"` | + | 3 | Fora (resgate) |
| Inter | `Aplicação - Estorno: "CDB PORQUINHO <TITULAR>"` | + | 1 | Fora (resgate) |
| Inter | `Credito Tesouro Direto: "* PROV * VENDA TD NTN-B1"` | + | 1 | Fora (resgate) |
| Inter | `Pix enviado: "Cp :18236120-<TITULAR>"` | − | 7 | Fora (entre contas) |
| Inter | `Pix recebido: "Cp :18236120-<TITULAR>"` / `"Cp :90400888-<TITULAR>"` | + | 29 | Fora (entre contas) |
| Nubank | `Aplicação RDB` | − | 4 | Investimento |
| Nubank | `Dinheiro guardado com resgate planejado` (Caixinha) | − | 1 | Investimento |
| Nubank | `Resgate RDB` (resgate de Caixinha, set/2026) | + | 2 | Fora (resgate) |
| Nubank | `Transferência recebida pelo Pix - <TITULAR> - •••.000.000-•• - BANCO INTER (0077) …` | + | 6 | Fora (entre contas) |
| Nubank | `Transferência enviada pelo Pix - <TITULAR> - •••.000.000-•• - BANCO INTER (0077) …` | − | 3 | Fora (entre contas) |
| Nubank | `Transferência Recebida - <OUTRA PESSOA> - …` | + | 1 | **Sem mudança** |
| Ambos | `Compra no debito…`, `Pix enviado/recebido` para terceiros, `Pagamento efetuado…`, `Pagamento de fatura`, `Pix enviado devolvido` | ± | ~145 | **Sem mudança** |

**Decisões tomadas com o usuário**
1. **Resgate** (inclui estorno de aplicação e venda/crédito de Tesouro Direto) entra **fora da importação**. Ele não conta
   como receita e não altera o total investido. O modelo continua o mesmo: nada de investimento negativo.
2. A detecção define **só o tipo e a inclusão**. A categoria fica vazia, a menos que o histórico do usuário traga uma.
   Nada de categoria padrão e nada de criar categorias automaticamente.
3. **O histórico do usuário vence.** Uma regra aprendida ou o histórico do **mesmo estabelecimento** têm prioridade sobre
   qualquer detecção. As detecções vencem o padrão por sinal e as sugestões por "nome parecido".
4. **Transferência entre contas próprias** entra **fora da importação** nos dois sentidos. O titular é o **nome do
   cadastro** do usuário (`User.Name`). Não há campo novo, migration nem tela nova.
5. O "RENDIMENTO LIQUIDO" do Nubank (`BALLIST`) é um saldo informativo e continua ignorado.

### Critérios de aceite

**Investimentos**
- **CA1.** Uma linha de saída cuja descrição normalizada **começa** com um padrão de aplicação recebe:
  `Type = Investment`, `Included = true`, `SuggestionSource = InvestmentApplication` e `SuggestionConfidence = High`.
- **CA2.** Uma linha de entrada cuja descrição normalizada **começa** com um padrão de resgate, ou de aplicação (estorno),
  recebe: `Included = false`, `Type = Income` (sem mudança), `SuggestionSource = InvestmentRedemption` e
  `SuggestionConfidence = High`.
- **CA3.** O sentido é obrigatório. `Aplicação - Estorno…` com valor positivo conta como resgate.
  `Dinheiro guardado com resgate planejado` com valor negativo conta como aplicação.

**Transferências entre contas próprias**
- **CA4.** O nome do titular é `User.Name` normalizado (`SearchText.Normalize`). Ficam de fora as palavras com 2 letras
  ou menos e as partículas `de`, `da`, `do`, `das`, `dos`, `e`. Se sobrarem **menos de 2 palavras**, essa detecção não
  roda.
- **CA5.** Uma linha é transferência entre contas próprias quando as duas condições abaixo valem:
  - a descrição normalizada **começa** com uma palavra de transferência: `pix`, `transferencia`, `transf`, `ted`, `doc`
    ou `tev`;
  - **todas** as palavras do titular aparecem como palavras inteiras na descrição, **na mesma ordem**. Com 3 palavras
    ou mais, o banco pode ter nomes a mais no meio ("Fulano Beltrano Ciclano" encontra "FULANO BELTRANO SOUZA
    CICLANO"). Com 2 palavras, elas precisam estar juntas: "Maria Silva" não encontra "Maria Aparecida Silva", que é
    outra pessoa. *(Revisado depois da revisão de código: antes, nomes a mais valiam também com 2 palavras.)*

  A linha então recebe `Included = false`, o tipo sem mudança, `SuggestionSource = OwnTransfer` e
  `SuggestionConfidence = High`.
- **CA6.** Um nome de terceiro não é detectado (`Transferência Recebida - <OUTRA PESSOA>`), e uma compra com o nome
  do titular que não começa com palavra de transferência também não é.

**Precedência e efeitos**
- **CA7.** Precedência em `SuggestCategoriesAsync`, por linha:
  1. uma sugestão do suggester com fonte `Rule` ou `History` (mesmo estabelecimento) é aplicada como hoje, e nenhuma
     detecção roda;
  2. senão, a detecção de **investimento**. Numa aplicação com sugestão `Similar` cujo `Type == Investment`, a categoria
     dessa sugestão é aproveitada e a fonte continua `InvestmentApplication`;
  3. senão, a detecção de **transferência entre contas próprias**;
  4. senão, a sugestão `Similar` (se houver) é aplicada como hoje.
- **CA8.** A detecção de duplicatas continua rodando depois e não muda.
- **CA9.** O aprendizado no commit continua igual. Um resgate ou transferência deixado de fora vira uma regra `Ignore`
  para o estabelecimento, e uma aplicação confirmada vira uma regra `Investment` com a categoria escolhida. Se o usuário
  **incluir** uma transferência entre contas (ex.: o salário que chega do Santander), a próxima importação respeita essa
  decisão pela regra 1 do CA7.
- **CA10.** As detecções não dependem da origem do arquivo: rodam no backend e valem para OFX e CSV.
- **CA11.** No frontend, as linhas detectadas mostram um selo no mesmo lugar dos selos de sugestão, e entram na contagem
  e no filtro **Sugeridas**:

  | Fonte | pt-BR | en-US |
  |---|---|---|
  | `InvestmentApplication` | "Investimento" | "Investment" |
  | `InvestmentRedemption` | "Resgate" | "Redemption" |
  | `OwnTransfer` | "Entre contas" | "Between accounts" |

  As linhas deixadas de fora aparecem também em **Excluídas**.

**Resultado nos arquivos de exemplo**
- **CA12.** Com o titular "Fulano Beltrano Ciclano" (o nome real do usuário nos arquivos) e sem histórico, aplicando as detecções às linhas dos três arquivos:
  - **23 aplicações** passam a Investimento (Inter 18, Nubank jan 3, Nubank set 2);
  - **20 resgates** saem da importação (Inter 18, Nubank set 2);
  - **45 transferências entre contas próprias** saem da importação (Inter 36, Nubank jan 4, Nubank set 5);
  - **nenhuma outra linha** muda de tipo ou de inclusão.

### Padrões

O texto é comparado depois de `SearchText.Normalize` (minúsculas, sem acentos). O padrão precisa aparecer **no começo** da
descrição, seguido de um separador (espaço, `:`, `-`, `"`) ou do fim do texto.

| Movimento | Prefixos | Sentido | Visto nos exemplos |
|---|---|---|---|
| Aplicação | `aplicacao`, `dinheiro guardado` | saída | sim |
| Aplicação | `aplic`, `compra tesouro direto`, `debito tesouro direto` | saída | não (formatos conhecidos) |
| Resgate | `resgate` (cobre `resgate rdb`), `credito tesouro direto`, `aplicacao` com valor positivo | entrada | sim |
| Resgate | `dinheiro resgatado`, `venda tesouro direto`, `estorno aplicacao` | entrada | não (formatos conhecidos) |
| Transferência | `pix`, `transferencia`, `transf`, `ted`, `doc`, `tev` + nome do titular | ambos | sim (`pix`, `transferencia`) |

## Tech Stack

- Backend `LifeManager`: .NET 10, C#, EF Core + Npgsql (PostgreSQL), xUnit com mocks escritos à mão.
- Frontend `LifeManagerFront`: React + TypeScript, Vite, Vitest, oxlint.
- Nenhuma dependência nova.

## Commands

```
# Backend (LifeManager/)
dotnet build LifeManager.slnx
dotnet test LifeManager.slnx
dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~InvestmentDetectorTests|FullyQualifiedName~OwnTransferDetectorTests"
dotnet test LifeManager.Application.Test --filter "FullyQualifiedName~StatementImportServiceTest"

# Frontend (LifeManagerFront/)
npm run build      # tsc -b && vite build
npm run lint       # oxlint
npm test           # vitest run
```

## Project Structure

**Backend**
- `LifeManager.Domain/StatementImports/InvestmentDetector.cs` (**novo**, puro): `Detect(string? description, decimal amount)` → `InvestmentMovement?`.
- `LifeManager.Domain/StatementImports/Enums/InvestmentMovement.cs` (**novo**): `Application`, `Redemption`.
- `LifeManager.Domain/StatementImports/OwnTransferDetector.cs` (**novo**, puro): é construído uma vez por importação a partir do nome do titular (`OwnTransferDetector.For(string? holderName)`) e expõe `IsOwnTransfer(string? description)`.
- `LifeManager.Domain/Categorization/Enums/SuggestionSource.cs`: novos valores `InvestmentApplication = 5`, `InvestmentRedemption = 6` e `OwnTransfer = 7`. A coluna é `integer`, então **não precisa de migration**.
- `LifeManager.Domain/StatementImports/StatementImportRow.cs`: novos métodos `ApplyInvestmentDetection(InvestmentMovement, CategoryId?)` e `ApplyOwnTransferDetection()`.
- `LifeManager.Application/StatementImports/Services/StatementImportService.cs`:
  - recebe `IUserRepository`, para ler `User.Name` uma vez por importação;
  - `SuggestCategoriesAsync` passa a seguir a precedência do CA7.
- Testes:
  - `LifeManager.Domain.Test/StatementImports/InvestmentDetectorTests.cs` e `OwnTransferDetectorTests.cs` (**novos**);
  - `StatementImportRowTests.cs`;
  - `LifeManager.Application.Test/StatementImports/StatementImportServiceTest.cs`, que precisa do usuário semeado no mock de usuários.
- Documentação:
  - `docs/regras-de-negocio.md`: a regra "resgate é registrado pelo usuário como receita" e a seção "Sugestão de categoria", com as detecções e a precedência;
  - `CLAUDE.md`: as seções MonthlySummaries (a mesma frase sobre resgate) e StatementImports.

**Frontend**
- `src/finance/types/StatementImportDtos.ts`: `SuggestionSource` passa a incluir `'InvestmentApplication' | 'InvestmentRedemption' | 'OwnTransfer'`.
- `src/finance/locales/pt-BR.ts` e `en-US.ts`: `imports.rows.suggestion.<fonte>`.
- `ImportRowList.tsx`: só muda se o selo de uma fonte detectada não deve mostrar "usado N vezes" (`suggestionUses = 0`).
- `docs/fluxo-de-telas.md`, se ele descreve os selos da revisão.
- O parser OFX não muda.

## Code Style

O código segue o estilo do domínio: classes puras, comentários `<summary>` curtos em inglês que explicam o motivo,
`static readonly` para listas fixas e nada de dependências.

```csharp
namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Recognizes the bank's own lines for money put into or taken out of an investment ("Aplicacao: CDB…", "Resgate RDB"),
    /// so a first import files them without any history. Only the start of the line counts, and the direction decides:
    /// money going out is an application, money coming in is a redemption (an application refunded included).
    /// </summary>
    public static class InvestmentDetector
    {
        private static readonly string[] ApplicationPrefixes = ["aplicacao", "aplic", "dinheiro guardado", /* … */];
        private static readonly string[] RedemptionPrefixes = ["resgate", "dinheiro resgatado", "credito tesouro direto", /* … */];

        public static InvestmentMovement? Detect(string? description, decimal amount)
        {
            var text = SearchText.Normalize(description);
            if (amount < 0)
                return StartsWithAny(text, ApplicationPrefixes) ? InvestmentMovement.Application : null;

            return StartsWithAny(text, RedemptionPrefixes) || StartsWithAny(text, ApplicationPrefixes)
                ? InvestmentMovement.Redemption
                : null;
        }
    }
}
```

- Nomes em inglês no código. Documentação de regra de negócio em pt-BR (`regras-de-negocio.md`).
- Testes no padrão `Method_ShouldExpectedBehavior_WhenCondition`, com `Assert.*` e mocks escritos à mão.

## Testing Strategy

**Domínio**
- `InvestmentDetectorTests`: um `[Theory]` com todas as descrições de investimento da tabela de evidência, além de
  casos negativos tirados dos arquivos (`Compra no debito…`, `Pix enviado…`, `Pagamento de fatura`,
  `Pagamento efetuado: "Debito Automatico Fatura Cartao Inter"`, `Transferência recebida…`).
  - Casos de sentido: o estorno positivo vira resgate e "resgate planejado" negativo vira aplicação.
  - Casos de borda: texto vazio ou nulo, e prefixo no meio da frase (`Pix enviado: Aplicacao…` não é detectado).
- `OwnTransferDetectorTests`:
  - os formatos do Inter e do Nubank, nos dois sentidos, com o titular de 3 palavras;
  - um titular cadastrado com 2 palavras ("Fulano Ciclano") detecta só com as duas juntas, e um de 3 aceita nomes a mais no meio;
  - um titular de 1 palavra ou vazio não detecta nada;
  - um nome de terceiro não é detectado;
  - palavras fora de ordem não são detectadas;
  - nome sem palavra de transferência no começo não é detectado;
  - acentos e caixa não importam;
  - as partículas (`da`, `dos`) são ignoradas.
- `StatementImportRowTests`: os dois métodos `Apply…Detection` definem tipo, inclusão, fonte, confiança e
  `SuggestionUses = 0`. Uma aplicação tem `SignedAmount` negativo.

**Aplicação** (`StatementImportServiceTest`)
- A precedência do CA7:
  - uma regra `Rule` de gasto para `aplicacao rdb` vence a detecção;
  - uma regra `Rule` de receita incluída para a transferência do titular vence `OwnTransfer`;
  - sem histórico, as linhas são detectadas;
  - uma sugestão `Similar` de investimento empresta a categoria.
- Um titular com nome de 1 palavra não deixa nenhuma transferência de fora.
- O CA12 com as linhas de investimento e de transferência mais uma amostra das linhas comuns dos três arquivos, todas
  anonimizadas e com o titular fictício trocado de forma coerente.

**Frontend**
- `npm run build` garante os tipos; as chaves novas existem nos dois locales.

**Manual**
- Importar os três OFX de exemplo e conferir a revisão: tipos, filtro Excluídas e selos.

`dotnet test LifeManager.slnx` e `npm test` precisam continuar verdes.

## Boundaries

- **Sempre:**
  - escrever os testes de domínio antes dos detectores (TDD);
  - manter os detectores puros (sem I/O);
  - atualizar `regras-de-negocio.md` e `CLAUDE.md` junto com o código;
  - anonimizar os dados dos extratos nos testes (nome, CPF parcial, agência/conta).
- **Perguntar antes:**
  - adicionar padrões além da tabela acima;
  - mudar o modelo de totais (investimento negativo, novo tipo de movimento);
  - criar migration ou campo novo de titular;
  - mexer no parser OFX do frontend.
- **Nunca:**
  - copiar os arquivos `.ofx` reais (ou CPF, nomes, contas) para o repositório;
  - deixar uma detecção passar por cima de uma regra aprendida ou do histórico do mesmo estabelecimento;
  - detectar investimento por palavra no meio da descrição;
  - detectar transferência própria com nome de 1 palavra.

## Success Criteria

1. Os CA1 a CA12 estão cobertos por testes automatizados que passam.
2. Com o cadastro igual ao nome do titular nos extratos (ou outro nome de pelo menos 2 palavras que apareçam nas linhas) e sem
   histórico, a importação dos três OFX de exemplo mostra as contagens do CA12 na revisão, com os selos certos. As outras
   linhas ficam como antes.
3. Depois de confirmar uma importação em que o usuário incluiu uma transferência própria como receita, ou reclassificou
   `aplicacao rdb` como gasto, uma nova importação respeita essa decisão (fonte `Rule`).
4. `dotnet test LifeManager.slnx`, `npm run build`, `npm run lint` e `npm test` passam.
5. A documentação de negócio descreve as três detecções, a precedência e que resgates e transferências entre contas
   próprias chegam fora da importação.

## Riscos conhecidos

- **Dinheiro vindo de uma conta própria que não é importada** (ex.: o salário que cai no Santander e é repassado ao
  Inter). Ele sai da importação como transferência e a receita some. Mitigação: a linha fica visível em Excluídas com o
  selo "Entre contas", e incluí-la uma vez ensina a regra para as próximas importações (CA9).
- **Nome de cadastro curto ou diferente do nome no banco** ("Rafa"). Nesse caso a detecção não roda e o aprendizado
  cobre depois. Não gera falso positivo.

## Open Questions

Nenhuma no momento.
