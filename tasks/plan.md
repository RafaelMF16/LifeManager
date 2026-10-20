# Implementation Plan: Detecção de investimentos e transferências entre contas próprias

Spec: [`../SPEC.md`](../SPEC.md) (aprovado). Lista de tarefas: [`todo.md`](todo.md).

## Overview

Duas detecções puras no domínio rodam na criação do rascunho de importação, depois das sugestões por histórico:
- `InvestmentDetector`: aplicação → Investimento; resgate → fora.
- `OwnTransferDetector`: Pix/transferência no nome do titular → fora.

Elas marcam a linha com novas fontes de sugestão (`InvestmentApplication`, `InvestmentRedemption`, `OwnTransfer`).
No frontend, essas fontes aparecem como selos na revisão. Não há migration, endpoint novo nem mudança no parser.

## Architecture Decisions

- **Os detectores ficam no backend, no domínio, e são puros.** Ficam ao lado do `DuplicateDetector` em
  `Domain/StatementImports/`. Valem para OFX e CSV e são testados sem DI, como `MerchantKey` e `CategorySuggester`.
- **Não entram no `CategorySuggester`.** O suggester responde "o que o usuário fez antes". As detecções são regras fixas.
  A precedência (CA7) fica em `StatementImportService.SuggestCategoriesAsync`, que já é o ponto que aplica sugestões às linhas.
  Para isso o service precisa saber se a sugestão veio do mesmo estabelecimento (`Rule`/`History`) ou de `Similar`, e
  `CategorySuggestion.Source` já informa isso. **O suggester não muda.**
- **Três valores novos em `SuggestionSource`, em vez de um campo novo na linha.** A coluna é `integer`, então não precisa
  de migration. Os filtros/contagens de "Sugeridas" (`SuggestionSource != None`) passam a incluir as linhas detectadas
  sem nenhuma mudança na query. Os três valores entram de uma vez na Tarefa 2, para o frontend tratar tudo numa tarefa só.
- **Métodos de domínio próprios na linha** (`ApplyInvestmentDetection`, `ApplyOwnTransferDetection`), em vez de montar
  um `CategorySuggestion` falso. Uma detecção não tem `Uses` nem `RuleAction`, e forçar esse formato confundiria o
  aprendizado e o selo.
- **O titular vem de `IUserRepository.GetByIdAsync`, uma vez por importação.** `OwnTransferDetector.For(name)` já monta
  as palavras normalizadas. Um nome com menos de 2 palavras significativas devolve um detector que nunca detecta.
- **O selo de "deixado de fora antes" precisa mudar.** Hoje `ImportRowList.tsx` mostra "Você deixou de fora antes"
  para qualquer linha sugerida, excluída e sem categoria. Uma linha detectada como resgate ou como transferência cairia
  nesse caso com o texto errado. As fontes detectadas passam a mostrar o próprio selo, sem confiança alta e sem
  "usado N vezes".

## Dependency Graph

```
SuggestionSource (+3 valores)
   │
   ├── InvestmentDetector ──┐
   │                        ├── StatementImportRow.Apply*Detection
   ├── OwnTransferDetector ─┘          │
   │                                   └── StatementImportService.SuggestCategoriesAsync (precedência, IUserRepository)
   │                                              │
   │                                              └── Teste CA12 (amostras dos 3 arquivos)
   │
   └── Frontend: tipo SuggestionSource + locales + selos em ImportRowList
                                                   │
                                                   └── Docs (regras-de-negocio, CLAUDE.md, fluxo-de-telas)
```

## Task List

### Phase 1: Investimentos de ponta a ponta
- [x] Tarefa 1: `InvestmentDetector` (domínio, TDD)
- [ ] Tarefa 2: novas fontes de sugestão e `StatementImportRow.ApplyInvestmentDetection`
- [ ] Tarefa 3: precedência no `StatementImportService` para investimentos
- [ ] Tarefa 4: selos das detecções na revisão (frontend)

### Checkpoint A: Investimentos
- [ ] `dotnet test LifeManager.slnx` verde; `npm run build`, `npm run lint` e `npm test` verdes
- [ ] Manual: importar o OFX do Inter numa conta sem histórico e ver 18 linhas como Investimento e 18 em Excluídas
      com o selo "Resgate"
- [ ] Revisão com o usuário

### Phase 2: Transferências entre contas próprias
- [ ] Tarefa 5: `OwnTransferDetector` (domínio, TDD)
- [ ] Tarefa 6: `ApplyOwnTransferDetection` e o titular no `StatementImportService`

### Checkpoint B: Transferências
- [ ] Suítes verdes
- [ ] Manual: importar os dois OFX do Nubank e ver as transferências próprias em Excluídas com o selo "Entre contas"
      e a transferência de terceiro sem mudança

### Phase 3: Validação com os arquivos reais e documentação
- [ ] Tarefa 7: teste do CA12 com amostras anonimizadas dos três arquivos
- [ ] Tarefa 8: documentação (regras de negócio, CLAUDE.md, fluxo de telas)

### Checkpoint: Completo
- [ ] Todas as suítes, o build e o lint verdes
- [ ] Os CA1 a CA12 cobertos
- [ ] Manual: os três OFX dão as contagens do CA12; depois de confirmar uma importação incluindo uma transferência
      própria, a reimportação respeita a decisão
- [ ] Pronto para revisão

## Parallelization

- As Tarefas 1 e 5 (detectores puros) não dependem uma da outra.
- A Tarefa 4 (frontend) só depende do contrato da Tarefa 2 (os nomes das fontes).
- As Tarefas 3 e 6 mexem no mesmo método e no mesmo arquivo de teste, então ficam **sequenciais**.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| O nome do cadastro do usuário não bate com o nome no banco (apelido, uma palavra só) | Médio: as transferências próprias não são detectadas | Sem falso positivo, e o aprendizado cobre depois. Conferir o nome cadastrado no Checkpoint B |
| Dinheiro que vem de uma conta própria não importada (salário do Santander) sai como transferência | Médio: a receita some | Fica visível em Excluídas com o selo "Entre contas"; incluir uma vez ensina a regra (CA9, testado na Tarefa 6) |
| O selo "Você deixou de fora antes" aparece em linhas detectadas | Baixo: texto errado na revisão | Tratado na Tarefa 4 |
| Dados pessoais dos extratos entram nos testes | Alto: vazamento no repositório | As amostras usam `<TITULAR>` fictício e CPF/conta trocados; os `.ofx` nunca são copiados (revisar no diff da Tarefa 7) |
| Um padrão de prefixo largo (`aplic`, `pix`) pega linhas indevidas | Baixo | Prefixo só no começo, seguido de separador, com casos negativos dos arquivos reais nos testes |

## Open Questions

Nenhuma.
