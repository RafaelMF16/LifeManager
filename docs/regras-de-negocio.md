# Regras de negócio

Este documento descreve **o que** o LifeManager garante em cada área, em linguagem de produto. Ele não explica **como** isso é implementado (classes, tabelas, índices); para isso, veja o [CLAUDE.md](../CLAUDE.md).

Quem mantém o código: ao mudar uma regra, atualize este documento junto.

## Sumário

- [Princípios gerais](#princípios-gerais)
- [Contas e sessão](#contas-e-sessão)
- [Finanças](#finanças)
  - [Categorias](#categorias)
  - [Meses](#meses)
  - [Lançamentos](#lançamentos)
  - [Transações recorrentes](#transações-recorrentes)
  - [Metas](#metas)
  - [Painel](#painel)
- [Hábitos](#hábitos)
  - [Personagem](#personagem)
  - [Cadastro de hábitos](#cadastro-de-hábitos)
  - [Frequências](#frequências)
  - [Check-in (hábitos para construir)](#check-in-hábitos-para-construir)
  - [Recaídas (hábitos para evitar)](#recaídas-hábitos-para-evitar)
  - [Ofensiva, marcos e proteções](#ofensiva-marcos-e-proteções)
  - [Fechamento do dia](#fechamento-do-dia)
  - [Estatísticas e extrato](#estatísticas-e-extrato)
- [Loja de recompensas](#loja-de-recompensas)
- [Apêndice: códigos de erro](#apêndice-códigos-de-erro)

## Princípios gerais

- **Cada usuário só vê os próprios dados.** Toda leitura e escrita é limitada ao usuário autenticado. Um id de outro usuário se comporta como se não existisse ("não encontrado").
- **"Hoje" é o dia no fuso do negócio** (padrão: `America/Sao_Paulo`), e não o relógio do navegador. Isso vale para check-ins, recorrências, resgates e o ano corrente dos meses.
- **Listas são paginadas no servidor:** 20 itens por página por padrão e no máximo 100.
- **A busca ignora acentos e maiúsculas:** "mercado" encontra "Mercadó".
- **Valores em dinheiro** são sempre positivos, com no máximo 2 casas decimais e até R$ 999.999.999.999,99. O tipo do lançamento é que diz se o dinheiro entra ou sai.

## Contas e sessão

**Cadastro**
- Nome obrigatório, até 100 caracteres.
- E-mail obrigatório, em formato válido e único no sistema.
- Senha de 8 a 50 caracteres.

**Login**
- E-mail ou senha errados retornam sempre o mesmo erro, sem revelar qual dos dois está errado.

**Sessão**
- O acesso vale **15 minutos** e é renovado automaticamente enquanto o usuário usa o app.
- A renovação é **deslizante por 7 dias**: quem fica até 7 dias sem abrir o app continua logado.
- Toda sessão tem um **teto de 30 dias** a partir do login. Depois disso é preciso entrar de novo, mesmo com uso contínuo.
- Cada renovação troca a credencial por uma nova. Se uma credencial antiga for reapresentada, o sistema trata como roubo e **encerra todas as sessões** do usuário.
- Credencial expirada, revogada, reutilizada ou inexistente recebe sempre a mesma resposta, sem revelar o motivo.

**Logout**
- Encerra a sessão no servidor. Pode ser repetido sem erro, mesmo se a sessão já tiver acabado.
- O acesso já emitido continua válido até expirar (no máximo 15 minutos).

**Preferências**
- Tema (claro ou escuro) e idioma (português ou inglês) ficam salvos por usuário e são aplicados no login.

## Finanças

### Categorias

- Uma categoria tem só um nome, de até 50 caracteres.
- O nome é **único por usuário, ignorando acentos e maiúsculas**: "Mercado", "mercado" e "Mercadó" são o mesmo nome. Renomear para outra grafia do mesmo nome é permitido.
- A categoria não tem tipo: a mesma categoria serve para gastos, receitas e investimentos.
- **Não é possível excluir uma categoria em uso** por lançamentos ou transações recorrentes. Antes, troque a categoria deles.
- Excluir uma categoria **apaga as metas** dela, porque uma meta não faz sentido sem a categoria.

### Meses

- Os lançamentos ficam dentro de um mês. Cada usuário tem **no máximo um registro por mês**.
- Pelo app, **só é possível criar meses do ano corrente**. Meses de anos passados só aparecem quando já existiam ou quando uma recorrência os abre (veja abaixo).
- Um mês novo começa com os totais zerados.
- **Totais:** receitas, gastos e investido nunca são negativos e só mudam pelos lançamentos.
- **Saldo = receitas − gastos − investido.** Investir é dinheiro guardado: ele sai da conta como um gasto, mas fica separado dos gastos. Um resgate de investimento é registrado pelo usuário como receita.
- A lista de meses filtra por ano e por saldo (positivo = saldo ≥ 0; negativo = saldo < 0). A ordenação padrão é do mais recente para o mais antigo.
- O detalhe do mês mostra a quantidade de lançamentos de cada tipo e o mês anterior e o próximo do usuário (os meses cadastrados mais próximos, não necessariamente consecutivos).

### Lançamentos

- **Tipos:** gasto, receita ou investimento.
- **Descrição:** obrigatória, até 80 caracteres (espaços nas pontas são removidos).
- **Valor:** maior que zero, com no máximo 2 casas decimais.
- **Data:** precisa cair **dentro do mês** do lançamento. Um lançamento nunca muda de mês.
- **Categoria:** obrigatória e precisa ser do próprio usuário.
- **Totais sempre corretos:** os totais do mês são recalculados a cada criação, edição ou exclusão. Edições simultâneas no mesmo mês são enfileiradas, então os totais não ficam desencontrados.
- Lançamentos criados por uma recorrência ficam marcados como tal. Se a recorrência for excluída, eles continuam no mês.

### Transações recorrentes

Uma recorrência lança a mesma transação **todo mês, no dia escolhido** (salário, aluguel, assinatura, aporte).

- Tem tipo, categoria, valor e descrição com as mesmas regras dos lançamentos.
- **Dia do mês:** de 1 a 31. Em meses mais curtos, cai no último dia (dia 31 em fevereiro cai em 28 ou 29).
- **Início:** um mês que **não pode ser anterior ao mês atual** (não existe lançamento retroativo). Depois que a recorrência começou, o início não muda mais.
- **Fim:** opcional. Quando informado, não pode ser anterior ao início. Depois do último mês, a recorrência fica **Encerrada**.
- **Situações:** Ativa, Pausada ou Encerrada.
- **Lançamento automático:** um processo do servidor, que roda a cada hora e ao iniciar a API, lança tudo o que já venceu. Se o servidor ficou parado, os meses perdidos são lançados um a um. Uma recorrência **nunca é lançada duas vezes** no mesmo mês.
- **Lançamento imediato:** ao criar, editar ou retomar uma recorrência, o que já venceu é lançado na hora. Exemplo: criar hoje uma recorrência no dia 5, com hoje sendo dia 10, já lança a deste mês.
- Se o mês da ocorrência ainda não existir, ele é **criado automaticamente**, mesmo na virada de ano.
- **Pausar e retomar:** ao retomar, os meses em que ela esteve pausada **são pulados** (não são lançados depois).
- **Editar** muda apenas os próximos lançamentos; os que já foram feitos ficam como estão. Se a recorrência acabou de ser lançada pelo processo automático no momento da edição, a edição é recusada e precisa ser repetida.
- **Excluir** não apaga as transações já lançadas.

### Metas

Há dois tipos de meta mensal:

| Tipo | Significado | Cumprida quando |
|---|---|---|
| **Limite de gastos** | O máximo que se quer gastar no mês | gasto ≤ meta |
| **Alvo de investimento** | O mínimo que se quer investir no mês | investido ≥ meta |

- Uma meta pode ser de uma **categoria** ou do **total do mês** (todas as categorias daquele tipo).
- O valor segue as regras de dinheiro (maior que zero, 2 casas decimais).
- **A meta vale do mês escolhido em diante**, até ser mudada. Mudar ou remover uma meta a partir de um mês **não altera os meses anteriores**, que continuam com a meta que tinham. Exemplo: limite de R$ 800 desde janeiro, alterado para R$ 600 a partir de abril, dá janeiro a março com R$ 800 e abril em diante com R$ 600.
- Na edição, o tipo e a categoria de uma meta não mudam. Para isso, remova a meta e crie outra.
- **Situação de um limite de gastos** (exibida no app): dentro do limite; **perto do limite** a partir de 80% da meta; limite estourado acima de 100%.
- **Situação de um alvo de investimento:** abaixo do alvo ou alvo atingido.

### Painel

- Mostra um período de **meses inteiros**, de no máximo **36 meses**. O fim não pode ser anterior ao início.
- Compara com:
  - **Período anterior:** o mesmo número de meses logo antes. É usado em "Este mês", "Últimos N meses" e nos anos passados.
  - **Mesmo período do ano anterior:** os mesmos meses 12 meses antes. É usado em "Este ano" e só é permitido para períodos de até 12 meses.
- **Variação** = (atual − anterior) ÷ |anterior|. Quando o valor anterior é zero, não há variação.
- Meses sem lançamentos aparecem zerados.
- As quebras por categoria mostram as **8 maiores**; o resto é somado em "Outras", de modo que as partes sempre fecham com o total.
- **Metas no painel:** cada mês é avaliado com a meta que valia naquele mês. A contagem "dentro do limite em X de N meses fechados" **deixa de fora o mês em andamento**. As categorias mais distantes da meta aparecem primeiro (até 5).

## Hábitos

O módulo de hábitos é um jogo: cumprir hábitos dá moedas e XP, e falhar tira HP.

### Personagem

Cada usuário tem um personagem com **nível, XP, HP, moedas e proteções de ofensiva**.

- Começa no nível 1, com **100 HP** (o máximo), 0 moedas e nenhuma proteção.
- **Nível:** passar do nível N para o N+1 exige **100 × N XP** (100 XP para o nível 2, mais 200 para o 3, e assim por diante). **Subir de nível restaura todo o HP.**
- **HP** fica sempre entre 0 e 100.
- **Nocaute:** quando o HP chega a 0, o jogador **perde 20% das moedas** (arredondado para baixo) e o HP volta a 100. Nível e XP são mantidos.
- Toda mudança em moedas, XP ou HP fica registrada no **extrato** (veja [Estatísticas e extrato](#estatísticas-e-extrato)).

**Recompensas por dificuldade**

| Dificuldade | Moedas ao cumprir | XP ao cumprir | HP perdido ao falhar |
|---|---|---|---|
| Fácil | 5 | 10 | 5 |
| Médio | 10 | 20 | 8 |
| Difícil | 20 | 40 | 12 |

- Todo hábito cumprido (ou dia limpo) também **recupera 1 HP**.
- **Bônus de ofensiva nas moedas:** +10% a cada 7 dias de ofensiva, até +50% (35 dias ou mais). O resultado é arredondado.

### Cadastro de hábitos

- **Nome:** obrigatório, até 60 caracteres, único entre os hábitos **ativos** (ignorando acentos e maiúsculas). Um hábito arquivado pode ter o mesmo nome de um ativo.
- **Descrição:** opcional, até 200 caracteres.
- **Gatilho:** opcional, até 120 caracteres ("depois de tomar café, eu…").
- **Tipo:**
  - **Construir:** algo que se quer fazer. Cumprir rende moedas e XP; deixar de fazer custa HP.
  - **Evitar:** algo que se quer parar. Cada dia sem recaída rende moedas; uma recaída custa HP.
  - O tipo **não muda** depois que o hábito é criado.
- **Dificuldade:** fácil, médio ou difícil (veja a tabela acima).
- O hábito começa a contar **no dia em que é criado**.
- **Excluir = arquivar:** o hábito sai da lista e deixa de ser cobrado, mas o histórico é mantido. Um hábito arquivado não pode ser editado.
- **Restaurar** volta o hábito à lista com a **ofensiva zerada**. Os dias em que ele ficou arquivado nunca são cobrados. Se já existir um hábito ativo com o mesmo nome, a restauração é recusada.
- Mudanças na frequência valem a partir do próximo dia avaliado.

### Frequências

Os dois tipos de hábito aceitam as três frequências, mas o significado muda:

| Frequência | Hábito para construir | Hábito para evitar |
|---|---|---|
| **Todo dia** | Precisa ser feito todos os dias | Evitado todos os dias |
| **Dias fixos** (escolhidos de segunda a domingo; pelo menos um) | Precisa ser feito só nos dias escolhidos | Evitado só nos dias escolhidos. Nos outros dias está **liberado**: não há recaída, dano nem moedas |
| **Vezes por semana** (1 a 6) | Precisa ser feito N vezes na semana, em quaisquer dias | **Limite semanal:** até N recaídas na semana não custam nada; cada recaída além disso custa HP |

- A semana vai de **segunda a domingo**.
- Nos hábitos semanais, a ofensiva é contada em **semanas**.

### Check-in (hábitos para construir)

- O check-in só é possível em hábito ativo, para construir, num dia em que ele está agendado e a partir da data de início.
- **Só hoje ou ontem** podem receber check-in ou ter o check-in desfeito. Ontem fica aberto até o fim de hoje; depois disso o dia fecha.
- Um check-in por hábito por dia.
- **Recompensa:** moedas da dificuldade com o bônus de ofensiva (já contando o dia marcado), o XP da dificuldade e +1 HP.
- **Hábito "vezes por semana"** que já bateu a meta da semana: o check-in é aceito, mas **não rende nada**.
- **Desfazer** devolve exatamente o que foi ganho (moedas, XP, marco e proteção, veja abaixo). A exceção é o HP: o desfazer nunca derruba o jogador, então o HP para em 1.

### Recaídas (hábitos para evitar)

Hábitos para evitar não recebem check-in. **Cada dia sem recaída vira um "dia limpo"** quando o dia fecha.

- A recaída só pode ser registrada em hábito ativo, para evitar, num dia em que ele é evitado (não num dia liberado), **hoje ou ontem**, a partir da data de início.
- Uma recaída por hábito por dia.
- **Custo:** o HP da dificuldade. No limite semanal, as primeiras N recaídas da semana são grátis e só as seguintes custam HP.
- A recaída **zera a ofensiva na hora**.
- **Desfazer** devolve o HP perdido. Se a recaída causou um nocaute, as moedas perdidas no nocaute não voltam.

### Ofensiva, marcos e proteções

**Ofensiva** é a sequência atual de dias (ou semanas) cumpridos.

- Contam como sucesso: hábito feito, dia limpo e dia **protegido**.
- Dias não agendados são ignorados: não contam nem quebram a sequência.
- Um dia que ainda pode ser marcado (hoje ou ontem) não quebra a ofensiva.
- Uma falha ou uma recaída quebra a ofensiva.
- Nos hábitos semanais, a semana conta quando atinge a meta (para construir: N vezes; para evitar: 7 − N dias limpos). Ela quebra assim que não dá mais para atingir.
- O **recorde** de cada hábito (maior ofensiva) fica salvo.

**Marcos de ofensiva:** bônus único de moedas ao passar de:

| Marco | Bônus |
|---|---|
| 7 dias | 25 moedas |
| 30 dias | 100 moedas |
| 66 dias | 250 moedas |
| 100 dias | 500 moedas |

- Nos hábitos semanais, cada semana de ofensiva vale 7 dias para os marcos e para o bônus de moedas.

**Proteções de ofensiva**
- O jogador ganha **1 proteção a cada 7 dias de ofensiva**, podendo guardar no **máximo 2**.
- Se um hábito para construir não for feito, o fechamento do dia **usa uma proteção automaticamente**: o dia fica "protegido", a ofensiva continua e não há dano. Mas o dia protegido não rende moedas.
- Marcos e proteções são ganhos só por check-ins e dias limpos.

### Fechamento do dia

Um processo do servidor roda a cada hora (e ao iniciar a API) e **fecha os dias que não podem mais ser marcados**, até anteontem. Ele avalia cada hábito, dia a dia, em ordem:

| Situação | Resultado |
|---|---|
| Dia não agendado, anterior ao início, ou meio de semana de um hábito semanal | Nada acontece |
| Já tem check-in ou recaída | Nada acontece (já foi resolvido) |
| Hábito para construir (todo dia / dias fixos) não feito | Usa uma proteção, se houver. Senão, registra **falha** e aplica o dano da dificuldade |
| Hábito para evitar, dia em que é evitado, sem recaída | **Dia limpo**, recompensado como um check-in |
| Hábito para construir "N vezes por semana", no domingo da semana | Para cada vez que faltou: dano × vezes faltantes, ou **uma** proteção que cobre a semana inteira |
| Hábito para evitar com limite semanal, no domingo da semana | Dentro do limite: um dia limpo para cada dia sem recaída, com recompensa por dia. Acima do limite: nada (cada recaída extra já custou HP) |

- **No máximo 1 nocaute por rodada:** quem ficou dias sem abrir o app perde no máximo um nocaute. O dano que sobrar é perdoado. As falhas continuam registradas e continuam quebrando a ofensiva.
- Semanas que começaram antes da criação do hábito não são avaliadas.

### Estatísticas e extrato

**Detalhe do hábito**
- **Mapa das últimas 13 semanas** (91 dias). Cada dia tem um estado: antes do início, folga, feito, dia limpo, protegido, falhou, recaída, pendente ou sem registro. Uma falha nunca é deduzida: só aparece o que foi registrado.
- **Consistência:**
  - Hábitos diários e de dias fixos: parte dos dias agendados dos últimos 30 dias que foram cumpridos (dias protegidos ficam fora da conta).
  - Hábitos semanais: parte das últimas 4 semanas fechadas que bateram a meta ou ficaram dentro do limite.
- **Rumo ao automático:** total de dias cumpridos, rumo aos 66 dias (média para um hábito virar automático). Falhar um dia não zera esse total.
- Hábitos arquivados também têm estatísticas.

**Extrato** (histórico)
- Lista toda mudança em moedas, XP e HP, da mais recente para a mais antiga: hábito cumprido, falha, recaída, dia limpo, marco, resgate, nocaute, desfeito, proteção usada e subida de nível.
- Pode ser filtrado por hábito.

**Tela "Hoje"**
- Mostra os hábitos para construir agendados para hoje e os de **ontem que ficaram pendentes**. Um hábito semanal só aparece como pendente enquanto a meta da semana não foi batida.
- Mostra os hábitos para evitar em vigor hoje e os que estão **liberados** hoje.
- Se houve alguma falha nos últimos 7 dias, o app pode mostrar uma mensagem de boas-vindas de volta.

## Loja de recompensas

O próprio usuário cadastra as recompensas e as compra com as moedas ganhas nos hábitos.

- **Nome:** obrigatório, até 60 caracteres, único entre as recompensas **ativas**.
- **Preço:** de 1 a 100.000 moedas (número inteiro). Mudar o preço vale só para os próximos resgates.
- **Ícone:** opcional, escolhido de uma lista fixa.
- **Excluir = arquivar:** a recompensa sai da loja, mas os resgates continuam no histórico. Uma recompensa arquivada não pode ser editada nem resgatada. **Restaurar** é recusado se já existir uma ativa com o mesmo nome.
- **Resgatar** desconta o preço das moedas. O saldo é conferido no momento do resgate: sem moedas suficientes, nada acontece.
- O resgate guarda o nome, o ícone e o **preço pago**. Mudar a recompensa depois não altera o histórico.
- **Desfazer um resgate:** só **no mesmo dia** do resgate e só uma vez. O preço pago volta integralmente.
- **Ritmo de ganho:** a loja mostra quantos dias de hábitos cada recompensa custa, com base na média de moedas por dia ganhas com hábitos nos **últimos 14 dias**. Resgates e nocautes não entram nessa média.

## Apêndice: códigos de erro

A API responde a falhas esperadas com `{ code, message, type }`. O frontend usa o `code` para mostrar a mensagem traduzida. **Os códigos fazem parte do contrato e não devem ser renomeados.**

O `type` define o status HTTP: `Validation` → 400, `Unauthorized` → 401, `NotFound` → 404, `Conflict` → 409.

### Contas

| Código | Tipo | Quando acontece |
|---|---|---|
| `User.UserNameIsNullOrWhiteSpace` | Validation | Nome vazio no cadastro |
| `User.UserNameTooLong` | Validation | Nome com mais de 100 caracteres |
| `User.EmailIsNullOrWhiteSpace` | Validation | E-mail vazio |
| `User.EmailIsInvalid` | Validation | E-mail fora do formato |
| `User.PlainPasswordIsNullOrWhiteSpace` | Validation | Senha vazia |
| `User.PlainPasswordTooShort` | Validation | Senha com menos de 8 caracteres |
| `User.PlainPasswordTooLong` | Validation | Senha com mais de 50 caracteres |
| `User.EmailRegistered` | Conflict | E-mail já cadastrado |
| `User.InvalidCredentials` | Unauthorized | E-mail ou senha errados |
| `User.NotFound` | NotFound | Usuário não existe |
| `Auth.InvalidRefreshToken` | Unauthorized | Sessão expirada, revogada ou inválida (é preciso entrar de novo) |
| `UserPreferences.InvalidTheme` | Validation | Tema desconhecido |
| `UserPreferences.InvalidLanguage` | Validation | Idioma desconhecido |

### Listas

| Código | Tipo | Quando acontece |
|---|---|---|
| `Paging.InvalidPage` | Validation | Página menor que 1 |
| `Paging.InvalidPageSize` | Validation | Tamanho de página fora de 1 a 100 |

### Finanças

| Código | Tipo | Quando acontece |
|---|---|---|
| `Category.NameIsNullOrWhiteSpace` | Validation | Nome da categoria vazio |
| `Category.NameTooLong` | Validation | Nome com mais de 50 caracteres |
| `Category.NameAlreadyExists` | Conflict | Já existe categoria com esse nome (ignorando acentos e maiúsculas) |
| `Category.InUse` | Conflict | Exclusão de categoria usada por lançamentos ou recorrências |
| `Category.NotFound` | NotFound | Categoria não existe ou é de outro usuário |
| `MonthlySummary.InvalidMonth` | Validation | Mês fora de 1 a 12 |
| `MonthlySummary.YearNotCurrent` | Validation | Criação de mês fora do ano corrente |
| `MonthlySummary.AlreadyExists` | Conflict | O mês já foi criado |
| `MonthlySummary.NotFound` | NotFound | Mês não existe ou é de outro usuário |
| `Transaction.InvalidType` | Validation | Tipo diferente de gasto, receita ou investimento |
| `Transaction.DescriptionIsNullOrWhiteSpace` | Validation | Descrição vazia |
| `Transaction.DescriptionTooLong` | Validation | Descrição com mais de 80 caracteres |
| `Transaction.AmountNotPositive` | Validation | Valor zero ou negativo |
| `Transaction.AmountTooManyDecimals` | Validation | Mais de 2 casas decimais |
| `Transaction.AmountTooLarge` | Validation | Valor acima do máximo |
| `Transaction.DateOutsideMonth` | Validation | Data fora do mês do lançamento |
| `Transaction.NotFound` | NotFound | Lançamento não existe |
| `RecurringTransaction.InvalidDay` | Validation | Dia fora de 1 a 31 |
| `RecurringTransaction.InvalidStartMonth` / `InvalidEndMonth` | Validation | Mês de início ou fim em formato inválido |
| `RecurringTransaction.StartInPast` | Validation | Início antes do mês atual |
| `RecurringTransaction.EndBeforeStart` | Validation | Fim antes do início |
| `RecurringTransaction.StartLocked` | Conflict | Mudança do início de uma recorrência que já começou |
| `RecurringTransaction.AlreadyPaused` / `NotPaused` | Conflict | Pausar uma recorrência já pausada, ou retomar uma que não está pausada |
| `RecurringTransaction.ChangedConcurrently` | Conflict | A recorrência foi lançada durante a edição; é preciso tentar de novo |
| `RecurringTransaction.NotFound` | NotFound | Recorrência não existe |
| `Budget.InvalidType` | Validation | Meta que não é de gasto nem de investimento |
| `Budget.InvalidMonth` | Validation | Mês em formato inválido |
| `Budget.AmountNotPositive` / `AmountTooManyDecimals` / `AmountTooLarge` | Validation | Valor da meta inválido (mesmas regras dos lançamentos) |
| `Budget.NotFound` | NotFound | Nenhuma meta para remover daquele mês em diante |
| `FinanceDashboard.InvalidFrom` / `InvalidTo` | Validation | Mês do período em formato inválido |
| `FinanceDashboard.EndBeforeStart` | Validation | Fim do período antes do início |
| `FinanceDashboard.PeriodTooLong` | Validation | Período maior que 36 meses |
| `FinanceDashboard.InvalidComparison` | Validation | Tipo de comparação desconhecido |
| `FinanceDashboard.ComparisonTooLong` | Validation | Comparação com o ano anterior em período maior que 12 meses |

### Hábitos

| Código | Tipo | Quando acontece |
|---|---|---|
| `Habit.NameIsNullOrWhiteSpace` | Validation | Nome vazio |
| `Habit.NameTooLong` | Validation | Nome com mais de 60 caracteres |
| `Habit.DescriptionTooLong` | Validation | Descrição com mais de 200 caracteres |
| `Habit.TriggerTooLong` | Validation | Gatilho com mais de 120 caracteres |
| `Habit.InvalidKind` / `InvalidDifficulty` / `InvalidFrequencyType` | Validation | Tipo, dificuldade ou frequência desconhecidos |
| `Habit.WeekDaysRequired` | Validation | "Dias fixos" sem nenhum dia escolhido |
| `Habit.InvalidTimesPerWeek` | Validation | Vezes por semana fora de 1 a 6 |
| `Habit.InvalidFrequencyCombination` | Validation | Dias ou vezes informados para a frequência errada |
| `Habit.NameAlreadyExists` | Conflict | Já existe hábito **ativo** com esse nome (ao criar, renomear ou restaurar) |
| `Habit.Archived` | Conflict | Edição de hábito arquivado |
| `Habit.AlreadyArchived` / `NotArchived` | Conflict | Arquivar um hábito já arquivado, ou restaurar um que não está arquivado |
| `Habit.NotFound` | NotFound | Hábito não existe |
| `Habit.NotCheckable` | Validation | Check-in em hábito para evitar |
| `Habit.NotAvoidable` | Validation | Recaída em hábito para construir |
| `Habit.NotScheduled` | Validation | Check-in ou recaída num dia em que o hábito não está agendado (ou está liberado) |
| `Habit.CheckInOutsideWindow` | Validation | Check-in ou recaída fora de hoje/ontem, ou antes do início |
| `Habit.AlreadyCheckedIn` | Conflict | O dia já tem check-in |
| `Habit.CheckInNotFound` | NotFound | Desfazer um check-in que não existe |
| `Habit.AlreadyRelapsed` | Conflict | O dia já tem recaída |
| `Habit.RelapseNotFound` | NotFound | Desfazer uma recaída que não existe |
| `PlayerProfile.StreakFreezesFull` | Conflict | O jogador já tem o máximo de proteções |
| `PlayerProfile.NoStreakFreeze` | Conflict | Uso de proteção sem ter nenhuma |

### Loja

| Código | Tipo | Quando acontece |
|---|---|---|
| `Rewards.NameIsNullOrWhiteSpace` | Validation | Nome vazio |
| `Rewards.NameTooLong` | Validation | Nome com mais de 60 caracteres |
| `Rewards.InvalidCost` | Validation | Preço fora de 1 a 100.000 |
| `Rewards.IconTooLong` | Validation | Ícone inválido |
| `Rewards.NameAlreadyExists` | Conflict | Já existe recompensa **ativa** com esse nome |
| `Rewards.Archived` | Conflict | Edição ou resgate de recompensa arquivada |
| `Rewards.AlreadyArchived` / `NotArchived` | Conflict | Arquivar uma recompensa já arquivada, ou restaurar uma que não está arquivada |
| `Rewards.InsufficientCoins` | Conflict | Moedas insuficientes para o resgate |
| `Rewards.UndoOutsideWindow` | Validation | Desfazer um resgate que não é de hoje |
| `Rewards.AlreadyUndone` | Conflict | O resgate já foi desfeito |
| `Rewards.NotFound` / `RedemptionNotFound` | NotFound | Recompensa ou resgate não existe |
