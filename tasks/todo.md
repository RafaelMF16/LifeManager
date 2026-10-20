# Tasks: Detecção de investimentos e transferências entre contas próprias

Spec: [`../SPEC.md`](../SPEC.md) · Plano: [`plan.md`](plan.md)

Caminhos relativos a `LifeManager/`, salvo os marcados com `Front/` (`../LifeManagerFront/`).

---

## Phase 1: Investimentos de ponta a ponta

### ✅ Tarefa 1: `InvestmentDetector` (domínio, TDD)

**Description:** classe estática pura que recebe a descrição e o valor com sinal da linha e diz se ela é uma aplicação,
um resgate ou nenhum dos dois. Usa os prefixos da tabela "Padrões" do spec, sobre `SearchText.Normalize`. O prefixo só
vale no começo do texto, seguido de um separador ou do fim. O sentido do valor decide: saída é aplicação, entrada é
resgate, e isso inclui o estorno de aplicação.

**Acceptance criteria:**
- [x] Todas as descrições de investimento da tabela de evidência dão o movimento esperado (CA1, CA2 e CA3).
- [x] Os casos negativos dos arquivos não são detectados: compras, Pix de terceiros, `Pagamento efetuado…`,
      `Pagamento de fatura`, `Transferência…`, `Pix enviado: Aplicacao…` (prefixo no meio), texto vazio e nulo.
- [x] `Dinheiro guardado com resgate planejado` (−) dá aplicação; `Aplicação - Estorno: "CDB…"` (+) dá resgate.

**Verification:**
- [x] `dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~InvestmentDetectorTests"` (os testes são escritos antes e falham primeiro)
- [x] `dotnet build LifeManager.slnx`

**Dependencies:** nenhuma

**Files likely touched:**
- `LifeManager.Domain/StatementImports/InvestmentDetector.cs` (novo)
- `LifeManager.Domain/StatementImports/Enums/InvestmentMovement.cs` (novo)
- `LifeManager.Domain.Test/StatementImports/InvestmentDetectorTests.cs` (novo)

**Estimated scope:** S

---

### ✅ Tarefa 2: novas fontes de sugestão e `StatementImportRow.ApplyInvestmentDetection`

**Description:** adiciona `InvestmentApplication = 5`, `InvestmentRedemption = 6` e `OwnTransfer = 7` a
`SuggestionSource`, com os comentários `<summary>`. Cria `ApplyInvestmentDetection(InvestmentMovement, CategoryId?)`
na linha.
- Aplicação: `Type = Investment` (pelo `SetType`, para manter o `SignedAmount`), a categoria opcional e a linha incluída.
- Resgate: `Included = false` e o tipo sem mudança.
- Nos dois casos: a fonte correspondente, `Confidence = High` e `Uses = 0`.

**Acceptance criteria:**
- [x] A aplicação deixa a linha como Investimento, com `SignedAmount` negativo, incluída, com a fonte
      `InvestmentApplication` e `High`; a categoria passada é aplicada.
- [x] O resgate deixa a linha fora, como `Income`, sem categoria, com a fonte `InvestmentRedemption` e `High`.
- [x] `RuleLearning.From` continua gerando `Ignore` para um resgate excluído e `Categorize`/`Investment` para uma
      aplicação confirmada (CA9).

**Verification:**
- [x] `dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~StatementImportRowTests|FullyQualifiedName~RuleLearningTests"`

**Dependencies:** Tarefa 1

**Files likely touched:**
- `LifeManager.Domain/Categorization/Enums/SuggestionSource.cs`
- `LifeManager.Domain/StatementImports/StatementImportRow.cs`
- `LifeManager.Domain.Test/StatementImports/StatementImportRowTests.cs`
- `LifeManager.Domain.Test/Categorization/RuleLearningTests.cs` (se faltar cobertura para o CA9)

**Estimated scope:** S

---

### ✅ Tarefa 3: precedência no `StatementImportService` para investimentos

**Description:** `SuggestCategoriesAsync` passa a seguir os passos 1, 2 e 4 do CA7. O passo 3 entra na Tarefa 6.
1. Uma sugestão com fonte `Rule` ou `History` é aplicada como hoje.
2. Senão, uma detecção de investimento é aplicada. Numa aplicação com sugestão `Similar` de `Investment`, a categoria
   dessa sugestão é passada junto.
3. Senão, a sugestão `Similar` é aplicada como hoje.

Os duplicados continuam sendo marcados depois.

**Acceptance criteria:**
- [x] Sem histórico, `Aplicação RDB` (−) vira Investimento incluído e `Resgate RDB` (+) vai para fora. Os dois contam
      em `SuggestedCount`.
- [x] Uma regra `Rule` de gasto para `aplicacao rdb` vence a detecção (a linha continua gasto, com a fonte `Rule`).
- [x] Uma sugestão `Similar` de investimento com categoria empresta a categoria, e a fonte fica `InvestmentApplication`.
      Uma `Similar` de gasto não muda a detecção.
- [x] Os testes que já existem continuam verdes.

**Verification:**
- [x] `dotnet test LifeManager.Application.Test --filter "FullyQualifiedName~StatementImportServiceTest"`
- [x] `dotnet test LifeManager.slnx`

**Dependencies:** Tarefa 2

**Files likely touched:**
- `LifeManager.Application/StatementImports/Services/StatementImportService.cs`
- `LifeManager.Application.Test/StatementImports/StatementImportServiceTest.cs`

**Estimated scope:** S

---

### Tarefa 4: selos das detecções na revisão (frontend)

**Description:** `SuggestionSource` no frontend passa a incluir as três fontes novas, com textos em pt-BR e en-US (CA11).
Em `ImportRowList.tsx`, uma linha com fonte detectada mostra o próprio selo, em vez de "Você deixou de fora antes"
quando está fora e sem categoria, e sem "usado N vezes". As linhas detectadas e excluídas não ganham também o selo
genérico de "Fora".

**Acceptance criteria:**
- [ ] Os selos mostram "Investimento", "Resgate" e "Entre contas" (en-US: "Investment", "Redemption",
      "Between accounts").
- [ ] Um resgate ou transferência detectado nunca mostra "Você deixou de fora antes". Uma linha com regra `Ignore`
      aprendida continua mostrando esse texto.
- [ ] As chaves existem nos dois locales (o build falha se o tipo de locale exigir paridade).

**Verification:**
- [ ] `Front/`: `npm run build`, `npm run lint`, `npm test`
- [ ] Manual: importar o OFX do Inter e conferir os selos nas abas Todas, Sugeridas e Excluídas

**Dependencies:** Tarefa 2 (o contrato dos nomes); para a verificação manual, a Tarefa 3

**Files likely touched:**
- `Front/src/finance/types/StatementImportDtos.ts`
- `Front/src/finance/locales/pt-BR.ts`
- `Front/src/finance/locales/en-US.ts`
- `Front/src/finance/components/ImportRowList/ImportRowList.tsx`

**Estimated scope:** M

---

### Checkpoint A: Investimentos
- [ ] `dotnet test LifeManager.slnx` verde
- [ ] `Front/`: `npm run build`, `npm run lint` e `npm test` verdes
- [ ] Manual: o OFX do Inter numa conta sem histórico mostra 18 linhas como Investimento e 18 resgates em Excluídas
      com o selo "Resgate"
- [ ] Revisão com o usuário antes da Phase 2

---

## Phase 2: Transferências entre contas próprias

### Tarefa 5: `OwnTransferDetector` (domínio, TDD)

**Description:** `OwnTransferDetector.For(string? holderName)` normaliza o nome do titular e descarta palavras com 2
letras ou menos e as partículas `de`, `da`, `do`, `das`, `dos`, `e`. Com menos de 2 palavras, o detector nunca
detecta. `IsOwnTransfer(string? description)` exige as duas condições:
- a descrição normalizada começa com `pix`, `transferencia`, `transf`, `ted`, `doc` ou `tev`;
- todas as palavras do titular aparecem como palavras inteiras, na mesma ordem, podendo ter palavras no meio (CA4, CA5, CA6).

**Acceptance criteria:**
- [ ] Os formatos do Inter (`Pix enviado/recebido: "Cp :<n>-<TITULAR>"`) e do Nubank
      (`Transferência recebida/enviada pelo Pix - <TITULAR> - …`) são detectados com o titular de 3 palavras, e também
      com o titular "Nome Sobrenome" de 2 palavras.
- [ ] Não são detectados: o nome de terceiro, as palavras fora de ordem, uma linha sem palavra de transferência no começo
      (ex.: `Estorno: "CDB PORQUINHO <TITULAR>"`), um titular de 1 palavra, um titular vazio ou nulo.
- [ ] Acento e caixa não importam, e as partículas do nome são ignoradas.

**Verification:**
- [ ] `dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~OwnTransferDetectorTests"` (os testes são escritos antes)

**Dependencies:** nenhuma (pode rodar em paralelo à Phase 1)

**Files likely touched:**
- `LifeManager.Domain/StatementImports/OwnTransferDetector.cs` (novo)
- `LifeManager.Domain.Test/StatementImports/OwnTransferDetectorTests.cs` (novo)

**Estimated scope:** S

---

### Tarefa 6: `ApplyOwnTransferDetection` e o titular no `StatementImportService`

**Description:** cria `StatementImportRow.ApplyOwnTransferDetection()`, que deixa a linha fora com a fonte
`OwnTransfer`, `High` e `Uses = 0`, sem mudar o tipo. O `StatementImportService` passa a receber `IUserRepository`.
Em `CreateAsync`, ele lê `User.Name` uma vez e monta o `OwnTransferDetector`. Em `SuggestCategoriesAsync`, entra o
passo 3 do CA7: depois do investimento e antes de `Similar`.

**Acceptance criteria:**
- [ ] Com o usuário "Fulano Beltrano Ciclano" e sem histórico, `Pix recebido: "Cp :123-FULANO BELTRANO CICLANO"` (+) e
      `Transferência enviada pelo Pix - Fulano Beltrano Ciclano - …` (−) saem da importação com a fonte `OwnTransfer`.
- [ ] Com o usuário "Fulano" (1 palavra), nada é detectado como transferência própria.
- [ ] Uma regra `Rule` de receita incluída para a chave da transferência vence a detecção (CA9); a investimento vence a
      transferência quando as duas se aplicam.
- [ ] O teste da linha cobre `ApplyOwnTransferDetection`.

**Verification:**
- [ ] `dotnet test LifeManager.Domain.Test --filter "FullyQualifiedName~StatementImportRowTests"`
- [ ] `dotnet test LifeManager.Application.Test --filter "FullyQualifiedName~StatementImportServiceTest"` (com o usuário
      semeado em `UserSingleton` e o `Clear()` no construtor)
- [ ] `dotnet test LifeManager.slnx`

**Dependencies:** Tarefas 3 e 5

**Files likely touched:**
- `LifeManager.Domain/StatementImports/StatementImportRow.cs`
- `LifeManager.Domain.Test/StatementImports/StatementImportRowTests.cs`
- `LifeManager.Application/StatementImports/Services/StatementImportService.cs`
- `LifeManager.Application.Test/StatementImports/StatementImportServiceTest.cs`

**Estimated scope:** M

---

### Checkpoint B: Transferências
- [ ] `dotnet test LifeManager.slnx` verde
- [ ] O nome cadastrado na conta de teste tem pelo menos 2 palavras que aparecem nos extratos
- [ ] Manual: nos dois OFX do Nubank, as transferências próprias (jan 4, set 5) aparecem em Excluídas com o selo
      "Entre contas", e a transferência de terceiro fica como receita

---

## Phase 3: Validação com os arquivos reais e documentação

### Tarefa 7: teste do CA12 com amostras anonimizadas dos três arquivos

**Description:** um teste de aplicação monta uma importação com as descrições e os sinais dos três arquivos, depois de
passar pelo `describe(NAME, MEMO)` do frontend. A amostra inclui todas as linhas de investimento e de transferência
própria, e algumas de cada formato comum. O titular é fictício e foi trocado de forma coerente; CPF parcial, agência e
conta também foram trocados. O teste confere as contagens por destino e que as outras linhas ficaram como gasto ou
receita, incluídas.

**Acceptance criteria:**
- [ ] Contagens: 23 Investimento, 20 resgates fora, 45 transferências próprias fora (é possível usar uma linha por
      formato com multiplicidade, desde que o total bata).
- [ ] As linhas comuns da amostra (compras, Pix de terceiros, pagamentos, transferência de terceiro) ficam sem mudança.
- [ ] Nenhum dado pessoal real no diff: não aparecem o nome do usuário, o CPF parcial nem o número da conta.

**Verification:**
- [ ] `dotnet test LifeManager.Application.Test --filter "FullyQualifiedName~StatementImportServiceTest"`
- [ ] `git diff` revisado procurando nome, CPF e conta reais

**Dependencies:** Tarefa 6

**Files likely touched:**
- `LifeManager.Application.Test/StatementImports/StatementImportServiceTest.cs` (ou `StatementImportSamples.cs` novo, com as linhas)

**Estimated scope:** S

---

### Tarefa 8: documentação

**Description:** atualizar as regras e a referência técnica junto com o comportamento.

**Acceptance criteria:**
- [ ] `docs/regras-de-negocio.md`:
  - a regra "um resgate é registrado pelo usuário como receita" passa a dizer que um resgate detectado no extrato chega
    fora da importação;
  - a seção "Sugestão de categoria" descreve as duas detecções, o titular pelo nome do cadastro (mínimo 2 palavras) e a
    precedência.
- [ ] `CLAUDE.md`: as seções MonthlySummaries (a mesma frase) e StatementImports (rows, suggestions e tests)
      descrevem os detectores, as fontes novas, o `IUserRepository` no service e a precedência.
- [ ] `Front/docs/fluxo-de-telas.md` (≈ linha 312): os novos selos da revisão.

**Verification:**
- [ ] Ler os três trechos e compará-los com o comportamento implementado

**Dependencies:** Tarefas 4 e 6

**Files likely touched:**
- `docs/regras-de-negocio.md`
- `CLAUDE.md`
- `Front/docs/fluxo-de-telas.md`

**Estimated scope:** S

---

### Checkpoint: Completo
- [ ] `dotnet build LifeManager.slnx` e `dotnet test LifeManager.slnx` verdes
- [ ] `Front/`: `npm run build`, `npm run lint` e `npm test` verdes
- [ ] Os CA1 a CA12 do spec cobertos por testes
- [ ] Manual: os três OFX numa conta sem histórico mostram as contagens do CA12
- [ ] Manual: depois de confirmar uma importação incluindo uma transferência própria, a reimportação traz essa linha
      incluída (fonte `Rule`)
- [ ] Pronto para revisão
