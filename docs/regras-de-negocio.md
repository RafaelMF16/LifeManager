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
  - [Importação de extrato](#importação-de-extrato)
  - [Orçamento do mês](#orçamento-do-mês)
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

**Cadastro (em duas etapas, com confirmação por e-mail)**
- **1ª etapa:** o usuário informa nome (obrigatório, até 100 caracteres) e e-mail (obrigatório e em formato válido). A resposta é sempre a mesma, tenha o e-mail conta ou não:
  - **e-mail sem conta:** o cadastro fica pendente e um e-mail de confirmação é enviado, no idioma da tela de cadastro, com um link que vale por **24 horas**;
  - **e-mail que já tem conta:** nada é criado, e o dono recebe um e-mail avisando que já tem conta, com o link para recuperar a senha.
- **2ª etapa:** o link abre a tela de definir senha (8 a 50 caracteres, digitada duas vezes). Ao salvar, a conta é criada e o usuário volta para o login (não entra automaticamente).
- Cadastrar de novo o mesmo e-mail ainda pendente renova o cadastro (nome, tema e idioma novos) e envia um link novo; **o link anterior deixa de valer**.
- O link só pode ser usado uma vez. Link vencido, já usado ou desconhecido recebe sempre a mesma resposta ("link inválido ou expirado").
- A conta nova já vem com as **preferências** (tema e idioma que a tela de cadastro estava usando; sem essa informação, claro e português) e as **[categorias padrão](#categorias)** nesse idioma. A conta, as preferências e as categorias são criadas juntas: ou tudo é gravado, ou nada.
- Se, entre o envio e a confirmação, o e-mail virar conta por outro caminho, o link é tratado como inválido.

**Recuperação de senha**
- Em "Esqueceu a senha?", o usuário informa o e-mail. A resposta é sempre a mesma: só um e-mail com conta recebe o link, e um e-mail sem conta não recebe nada.
- O link vale por **1 hora** e só pode ser usado uma vez. Pedir um link novo invalida os anteriores ainda não usados.
- Ao salvar a nova senha, **todas as sessões abertas da conta são encerradas** (em qualquer aparelho) e o usuário volta para o login.
- O e-mail vai no idioma das preferências da conta.

**Limites dos e-mails**
- Cada tipo de e-mail (confirmação, "você já tem uma conta" e recuperação) vai para o mesmo endereço **no máximo uma vez por minuto**. Um pedido dentro desse minuto responde normalmente, mas não envia nada; o link já enviado continua valendo.
- Por endereço de rede (IP): no máximo **10 pedidos de cadastro ou recuperação** e **20 usos de link** (verificar, concluir cadastro ou redefinir senha) **a cada 10 minutos**. Acima disso, a resposta é "muitas tentativas" até a janela virar.
- O tempo de resposta pode variar um pouco entre e-mail com conta e sem conta na recuperação (só o primeiro envia e-mail). Foi aceito para o porte do projeto.

**Limpeza automática**
- De hora em hora são apagados: cadastros pendentes vencidos há mais de **7 dias**, links de recuperação vencidos ou usados há mais de **7 dias** e os registros do limite de um minuto com mais de **1 dia**.

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
- **Toda conta nova começa com 12 categorias**, no idioma do cadastro: Moradia, Mercado, Restaurantes, Transporte, Saúde, Educação, Lazer, Assinaturas, Compras, Salário, Investimentos e Outros (em inglês: Housing, Groceries, Restaurants, Transportation, Health, Education, Leisure, Subscriptions, Shopping, Salary, Investments e Other). São categorias comuns do usuário: dá para renomear e excluir como qualquer outra, e elas não mudam de idioma se ele trocar o idioma depois. Contas criadas antes dessa regra não as recebem.
- O nome é **único por usuário, ignorando acentos e maiúsculas**: "Mercado", "mercado" e "Mercadó" são o mesmo nome. Renomear para outra grafia do mesmo nome é permitido.
- A categoria não tem tipo: a mesma categoria serve para gastos, receitas e investimentos.
- **Não é possível excluir uma categoria em uso** por lançamentos ou transações recorrentes. Antes, troque a categoria deles.
- Excluir uma categoria **apaga as fatias de orçamento** dela, em todos os meses, porque uma fatia não faz sentido sem a categoria. O % delas volta para o Livre.

### Meses

- Os lançamentos ficam dentro de um mês. Cada usuário tem **no máximo um registro por mês**.
- Pelo app, **só é possível criar meses do ano corrente**. Meses de anos passados só aparecem quando já existiam, quando uma recorrência os abre ou quando uma [importação de extrato](#importação-de-extrato) é confirmada.
- Um mês novo começa com os totais zerados.
- **Totais:** receitas, gastos e investido nunca são negativos e só mudam pelos lançamentos.
- **Saldo = receitas − gastos − investido.** Investir é dinheiro guardado: ele sai da conta como um gasto, mas fica separado dos gastos. Um resgate de investimento não é receita: na [importação de extrato](#importação-de-extrato) ele já vem fora da importação (o usuário pode incluí-lo, como receita, se quiser).
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
- **Salário pago no fim do mês anterior** (o salário de setembro cai no último dia útil de agosto): use o **dia 1**. A recorrência lança em 01/09, dentro de setembro, e a linha do extrato marcada como "conta no mês seguinte" (veja [Importação de extrato](#importação-de-extrato)) é reconhecida como duplicata desse lançamento.
- **Editar** muda apenas os próximos lançamentos; os que já foram feitos ficam como estão. Se a recorrência acabou de ser lançada pelo processo automático no momento da edição, a edição é recusada e precisa ser repetida.
- **Excluir** não apaga as transações já lançadas.

### Importação de extrato

Traz o extrato do banco (OFX ou CSV) para o app. O arquivo é lido no navegador; o servidor recebe as linhas.

**Rascunho**
- Enviar um extrato cria uma **importação em revisão**: nada é lançado até o usuário confirmar. A revisão fica salva e pode ser retomada depois, em qualquer dispositivo.
- Cada importação tem **de 1 a 5.000 linhas**. Cada linha precisa de descrição, de um valor diferente de zero (com no máximo 2 casas decimais) e de uma data **entre 01/01/2000 e hoje**; uma linha inválida recusa o envio inteiro.
- Valor negativo vira **gasto** e positivo vira **receita**; a descrição do banco é cortada em 80 caracteres (o texto original, até 255, fica guardado para consulta).
- A mesma linha do banco repetida no arquivo (mesmo identificador da linha e da conta, no OFX) é considerada uma vez só.
- Uma importação em revisão há mais de **30 dias** é apagada automaticamente.

**Revisão**
- Cada linha pode ser editada como um lançamento (tipo, categoria, valor, descrição, data) e **incluída ou deixada de fora**. A categoria pode ficar vazia durante a revisão; a data pode mudar para qualquer mês.
- Várias linhas selecionadas (até 100 de uma vez) podem receber a mesma categoria, o mesmo tipo ou ser incluídas/deixadas de fora juntas.
- **Aplicar às parecidas:** a decisão de uma linha (tipo, categoria, incluída ou não, mês seguinte ou não) pode ser copiada para as outras linhas do mesmo estabelecimento. Linhas que o usuário editou uma a uma não mudam, e uma possível duplicata continua de fora.
- Depois de confirmada, uma importação não muda mais.

**Conta no mês seguinte** (salário ou conta paga no fim do mês que pertence ao mês seguinte)
- Qualquer linha (receita, gasto ou investimento) pode ser marcada como **"conta no mês seguinte"**. Ela guarda a data do banco, mas conta e é lançada no **dia 1º do mês seguinte**: o salário de 30/08 vira um lançamento de 01/09, em setembro. Uma linha de 31/12 vai para 01/01 do ano seguinte.
- A linha marcada aparece no mês seguinte na revisão (contagem, totais e filtro por mês). Cabe ao usuário decidir se a linha é do mês seguinte ou do mês da data.
- A marcação pode ser feita linha a linha, em várias linhas selecionadas ou por "aplicar às parecidas". Ao trocar a data de uma linha no formulário, a marcação é desfeita, e o usuário marca de novo se quiser.
- **Aprendizado:** na confirmação, cada linha incluída com data nos **últimos 5 dias do mês** ensina, para o seu estabelecimento e tipo, "mês seguinte" (se estava marcada) ou "mesmo mês" (se não estava). Linhas fora desses dias, ou deixadas de fora, não ensinam nada sobre o mês.
- Numa próxima importação, uma linha incluída do mesmo estabelecimento e tipo, com data nos últimos 5 dias do mês, **já vem marcada** quando "mês seguinte" pesa mais que "mesmo mês" (decisões recentes pesam mais, como nas sugestões de categoria; empate não marca). Ela vem com o selo "pelo histórico". Fora desses dias nada é marcado sozinho: um salário atrasado para o dia 2, ou outro pagamento do mesmo estabelecimento no meio do mês, fica no mês da data.

**Sugestão de categoria**
- Cada linha é associada a um **estabelecimento**: a descrição do banco sem datas, números, cartões, parcelas e prefixos de meio de pagamento ("COMPRA CARTAO 12/03 IFOOD *RESTAURANTE 1234" → "ifood restaurante").
- A sugestão vem, nesta ordem:
  1. do que o usuário **decidiu em importações confirmadas** para o mesmo estabelecimento (regras aprendidas, que valem o dobro);
  2. dos **lançamentos do usuário** com a mesma descrição (feitos à mão, por recorrência ou importados);
  3. do **texto do próprio banco** (veja "Reconhecidos pelo texto do banco" abaixo);
  4. de um estabelecimento com **nome parecido** (nunca para deixar de fora).
- Decisões recentes pesam mais (metade do peso depois de um ano). Só contam decisões no mesmo sentido do dinheiro: saída pode virar gasto ou investimento; entrada, receita.
- **Confiança:** alta quando a mesma escolha foi feita ao menos 2 vezes e soma pelo menos 80% das decisões daquele estabelecimento; média ou baixa nos outros casos (parecidos nunca passam de média).
- Se o usuário deixou um estabelecimento de fora antes (ex.: transferência entre contas próprias), a linha já vem **fora da importação**.

**Reconhecidos pelo texto do banco** (valem desde a primeira importação, sem histórico; o que o usuário já decidiu para o mesmo estabelecimento vem antes)
- **Aplicação** (saída que começa com "Aplicação", "Aplic", "Dinheiro guardado", "Compra/Débito Tesouro Direto"): vem como **investimento**. A categoria fica vazia, a menos que um investimento de nome parecido já tenha uma.
- **Resgate** (entrada que começa com "Resgate", "Dinheiro resgatado", "Crédito/Venda Tesouro Direto", ou uma aplicação estornada): vem **fora da importação**, porque não é receita nem dinheiro novo investido.
- **Transferência entre contas próprias** (Pix, TED ou transferência, nos dois sentidos, com o **nome do cadastro** do usuário na linha): vem **fora da importação**. Todas as palavras do nome do cadastro precisam aparecer, na mesma ordem. Com 3 palavras ou mais, o banco pode ter nomes a mais no meio; com 2, elas precisam estar juntas ("Maria Silva" não reconhece "Maria Aparecida Silva", que é outra pessoa). Palavras de até 2 letras e "de", "da", "do", "das", "dos", "e" não contam. Com um nome de uma palavra só, nada é reconhecido, para não pegar outras pessoas com o mesmo nome.
- Só vale o **começo** do texto: "Pix enviado: Aplicação…" não é uma aplicação. O sentido do dinheiro decide: uma aplicação com valor de entrada é um estorno, logo um resgate.
- Incluir uma dessas linhas (por exemplo, o salário que chega de uma conta própria que não é importada) ensina a regra, e as próximas importações seguem essa decisão.

**Possíveis duplicatas** (linhas que vêm de fora, para o usuário decidir)
- a mesma linha de um extrato já importado (mesmo identificador da linha e da conta);
- um lançamento do usuário com a **mesma data e o mesmo valor** (com o mesmo sentido);
- um lançamento com o mesmo valor **até 3 dias de distância** e um estabelecimento parecido.
- Cada lançamento existente corresponde a no máximo uma linha, então duas compras iguais no mesmo dia não somem as duas.
- Para uma linha que **já vem marcada** como "conta no mês seguinte", a data comparada é o dia 1º do mês seguinte: o salário de 30/08 é reconhecido no lançamento de 01/09 feito pela recorrência. As duplicatas são procuradas só ao enviar o extrato, então marcar uma linha à mão na revisão não procura de novo.

**Confirmação**
- Só é possível confirmar com **ao menos uma linha incluída** e **todas as incluídas com categoria** (de outro usuário não vale).
- Os meses que faltam são **criados, de qualquer ano** (exceção à regra do ano corrente), e os totais de cada mês são recalculados uma vez.
- Uma linha do banco já lançada naquele mês (por outra importação confirmada no meio tempo) é **pulada**; o resultado informa quantas.
- Duas confirmações da mesma importação ao mesmo tempo lançam uma vez só.
- A confirmação **ensina as sugestões:** cada linha incluída conta um uso da sua categoria para o estabelecimento, e cada linha deixada de fora (que não seja duplicata) conta como "deixar de fora". Também ensina o mês das linhas do fim do mês (veja "Conta no mês seguinte").
- Confirmada, a importação vira histórico (quantas transações e quantos meses novos); as linhas da revisão são apagadas.
- **Remover do histórico** uma importação confirmada não apaga as transações que ela lançou. Descartar uma em revisão apaga só a revisão.

### Orçamento do mês

Cada mês tem **no máximo um orçamento**: um **total** (quanto se pretende gastar e investir no mês) dividido em **fatias de %**.

| Fatia | Significado | Cumprida quando |
|---|---|---|
| **Gasto de uma categoria** | O máximo que se quer gastar nela no mês | gasto da categoria ≤ valor da fatia |
| **Investimentos** (no máximo uma, sem categoria) | O mínimo que se quer investir no mês | tudo o que foi investido ≥ valor da fatia |

- O total segue as regras de dinheiro (maior que zero, 2 casas decimais).
- Cada fatia tem um % **maior que 0 e até 100, com até 2 casas**. A **soma das fatias não passa de 100%**, e a mesma categoria não aparece duas vezes.
- **Valor de uma fatia** = total × % ÷ 100, arredondado em centavos.
- **Livre** = o que as fatias deixam: 100% − a soma dos %. Em dinheiro, é o total menos os valores das fatias, para fechar exatamente com o total. **Gastos em categorias sem fatia contam contra o Livre.**
- **Cada mês é independente:** salvar ou excluir o orçamento de um mês não muda nenhum outro. Salvar substitui o orçamento do mês inteiro.
- Mudar o total **mantém os %** e recalcula os valores das fatias.
- **Copiar:** um mês sem orçamento pode começar com uma cópia do **último mês anterior que tem orçamento** (não precisa ser o mês imediatamente anterior). A cópia só é gravada quando o usuário salva.
- Meses passados e futuros podem ser editados.
- **Situação de uma fatia de gasto** (exibida no app): dentro do limite; **perto do limite** a partir de 80% do valor; limite estourado acima de 100%. **Situação da fatia de investimentos:** abaixo do alvo ou alvo atingido.
- **Metas antigas:** as metas em R$ que valiam "do mês escolhido em diante" foram convertidas em orçamentos, do primeiro mês com meta até o mês da conversão. Em cada mês: total = o maior entre o limite do total do mês e a soma dos limites por categoria, mais o alvo de investimento (ou a soma dos alvos por categoria). Cada limite por categoria virou uma fatia, e o investimento virou uma fatia só. Os valores podem ter mudado em centavos por causa do arredondamento dos %.

### Painel

- Mostra um período de **meses inteiros**, de no máximo **36 meses**. O fim não pode ser anterior ao início.
- Compara com:
  - **Período anterior:** o mesmo número de meses logo antes. É usado em "Este mês", "Últimos N meses" e nos anos passados.
  - **Mesmo período do ano anterior:** os mesmos meses 12 meses antes. É usado em "Este ano" e só é permitido para períodos de até 12 meses.
- **Variação** = (atual − anterior) ÷ |anterior|. Quando o valor anterior é zero, não há variação.
- **Poupado = receitas − gastos.** No painel, investir conta como guardar, não como gastar. O poupado se divide em **investido** e **em conta** (o saldo, com a mesma regra do mês), e as duas partes sempre somam o total. A tela do mês e a lista de meses continuam mostrando o saldo.
- **Taxa de poupança** = poupado ÷ receitas do período. Fica negativa quando os gastos passam das receitas, e não existe quando não há receitas. Um resgate lançado como receita entra no poupado do mês em que foi lançado.
- Meses sem lançamentos aparecem zerados.
- As quebras por categoria mostram as **8 maiores**; o resto é somado em "Outras", de modo que as partes sempre fecham com o total.
- **Orçamento no painel:** cada mês é avaliado com o seu próprio orçamento. O **limite de gastos do mês** é o total menos a fatia de investimentos (o Livre conta como gasto permitido), e o **alvo de investimento** é a fatia de investimentos. Meses sem orçamento ficam sem meta. A contagem "dentro do limite em X de N meses fechados" **deixa de fora o mês em andamento**. As categorias que mais passaram do valor da fatia aparecem primeiro (até 5).
- **Gastos por dia:** o calendário do painel soma só os **gastos** de cada dia do mês (receitas e investimentos ficam de fora), pela data do lançamento, com o mesmo critério dos totais do mês, então os dias sempre fecham com o gasto do mês. Uma linha de extrato jogada para o mês seguinte conta no dia em que foi lançada (o dia 1º). Todos os dias do mês aparecem, zerados quando não há gasto.

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

O `type` define o status HTTP: `Validation` → 400, `Unauthorized` → 401, `NotFound` → 404, `Conflict` → 409. A exceção é `Auth.TooManyRequests`, que sai com 429.

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
| `User.InvalidCredentials` | Unauthorized | E-mail ou senha errados |
| `User.NotFound` | NotFound | Usuário não existe |
| `Auth.InvalidRefreshToken` | Unauthorized | Sessão expirada, revogada ou inválida (é preciso entrar de novo) |
| `Auth.InvalidEmailToken` | Validation | Link do e-mail (cadastro ou recuperação) vencido, já usado ou desconhecido |
| `Auth.TooManyRequests` | Failure (429) | Muitos pedidos de cadastro/recuperação ou usos de link do mesmo IP em 10 minutos |
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
| `Transaction.DateOutsideMonth` | Validation | Data fora do mês do lançamento (também ao filtrar a lista de um dia) |
| `Transaction.NotFound` | NotFound | Lançamento não existe |
| `RecurringTransaction.InvalidDay` | Validation | Dia fora de 1 a 31 |
| `RecurringTransaction.InvalidStartMonth` / `InvalidEndMonth` | Validation | Mês de início ou fim em formato inválido |
| `RecurringTransaction.StartInPast` | Validation | Início antes do mês atual |
| `RecurringTransaction.EndBeforeStart` | Validation | Fim antes do início |
| `RecurringTransaction.StartLocked` | Conflict | Mudança do início de uma recorrência que já começou |
| `RecurringTransaction.AlreadyPaused` / `NotPaused` | Conflict | Pausar uma recorrência já pausada, ou retomar uma que não está pausada |
| `RecurringTransaction.ChangedConcurrently` | Conflict | A recorrência foi lançada durante a edição; é preciso tentar de novo |
| `RecurringTransaction.NotFound` | NotFound | Recorrência não existe |
| `StatementImport.FileNameIsNullOrWhiteSpace` / `FileNameTooLong` | Validation | Nome do arquivo vazio ou com mais de 255 caracteres |
| `StatementImport.AccountKeyTooLong` | Validation | Identificador da conta com mais de 100 caracteres |
| `StatementImport.InvalidSource` | Validation | Formato diferente de OFX ou CSV |
| `StatementImport.Empty` / `TooManyLines` | Validation | Extrato sem linhas ou com mais de 5.000 |
| `StatementImport.AmountZero` | Validation | Linha com valor zero |
| `StatementImport.DateOutOfRange` | Validation | Linha com data antes de 2000 ou depois de hoje |
| `StatementImport.InvalidMonth` | Validation | Filtro de mês em formato inválido |
| `StatementImport.NothingToChange` / `TooManyRowsSelected` | Validation | Alteração em lote sem linhas ou sem mudança, ou com mais de 100 linhas |
| `StatementImport.NothingToImport` | Validation | Confirmação sem nenhuma linha incluída |
| `StatementImport.UncategorizedRows` | Validation | Confirmação com linhas incluídas sem categoria |
| `StatementImport.AlreadyCommitted` | Conflict | Mudança ou confirmação de uma importação já confirmada |
| `StatementImport.NotFound` / `RowNotFound` | NotFound | Importação (ou linha dela) não existe ou é de outro usuário |
| `BudgetPlan.InvalidMonth` | Validation | Mês em formato inválido |
| `BudgetPlan.InvalidType` | Validation | Fatia que não é de gasto nem de investimento |
| `BudgetPlan.TotalNotPositive` / `TotalTooManyDecimals` / `TotalTooLarge` | Validation | Total do orçamento inválido (mesmas regras dos lançamentos) |
| `BudgetPlan.PercentOutOfRange` / `PercentTooManyDecimals` | Validation | % de uma fatia fora de 0 (exclusivo) a 100, ou com mais de 2 casas |
| `BudgetPlan.OverAllocated` | Validation | As fatias somam mais de 100% |
| `BudgetPlan.DuplicateCategory` | Validation | A mesma categoria em duas fatias |
| `BudgetPlan.ExpenseSliceWithoutCategory` | Validation | Fatia de gasto sem categoria |
| `BudgetPlan.InvalidInvestmentSlice` | Validation | Fatia de investimento com categoria, ou mais de uma |
| `BudgetPlan.NotFound` | NotFound | O mês não tem orçamento para excluir |
| `FinanceDashboard.InvalidFrom` / `InvalidTo` | Validation | Mês do período em formato inválido |
| `FinanceDashboard.EndBeforeStart` | Validation | Fim do período antes do início |
| `FinanceDashboard.PeriodTooLong` | Validation | Período maior que 36 meses |
| `FinanceDashboard.InvalidComparison` | Validation | Tipo de comparação desconhecido |
| `FinanceDashboard.ComparisonTooLong` | Validation | Comparação com o ano anterior em período maior que 12 meses |
| `FinanceDashboard.InvalidMonth` | Validation | Mês dos gastos por dia ausente ou em formato inválido |

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
