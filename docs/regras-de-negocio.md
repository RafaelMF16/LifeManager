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
  - [Objetivos de economia](#objetivos-de-economia)
  - [Painel](#painel)
- [Hábitos](#hábitos)
  - [Personagem](#personagem)
  - [Cadastro de hábitos](#cadastro-de-hábitos)
  - [Frequências](#frequências)
  - [Check-in (hábitos para construir)](#check-in-hábitos-para-construir)
  - [Check-in automático (rotinas financeiras)](#check-in-automático-rotinas-financeiras)
  - [Meta diária (hábitos mensuráveis)](#meta-diária-hábitos-mensuráveis)
  - [Recaídas (hábitos para evitar)](#recaídas-hábitos-para-evitar)
  - [Economia (hábitos para evitar)](#economia-hábitos-para-evitar)
  - [Ofensiva, marcos e proteções](#ofensiva-marcos-e-proteções)
  - [Fechamento do dia](#fechamento-do-dia)
  - [Estatísticas e extrato](#estatísticas-e-extrato)
  - [Conquistas](#conquistas)
  - [Perfil do jogador](#perfil-do-jogador)
  - [Desafios da semana](#desafios-da-semana)
  - [Resumo da semana](#resumo-da-semana)
- [Loja de recompensas](#loja-de-recompensas)
  - [Itens de jogo](#itens-de-jogo)
- [Ranking em grupos](#ranking-em-grupos)
  - [Pontos da semana](#pontos-da-semana)
  - [Grupos](#grupos)
  - [Ranking do grupo](#ranking-do-grupo)
  - [Reações](#reações)
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
- De hora em hora são apagados: as contas cuja exclusão venceu (ver [Minha conta](#contas-e-sessão)), cadastros pendentes vencidos há mais de **7 dias**, links de recuperação vencidos ou usados há mais de **7 dias**, os registros do limite de um minuto com mais de **1 dia** e as tentativas de login cuja janela de 15 minutos acabou há mais de **1 dia**.

**Login**
- E-mail ou senha errados retornam sempre o mesmo erro, sem revelar qual dos dois está errado. A resposta também leva o mesmo tempo com ou sem conta para o e-mail.
- **Senhas erradas:** **5 senhas erradas** para o mesmo e-mail, vindas do mesmo endereço de rede (IP), em **15 minutos** contados da primeira, bloqueiam esse e-mail **naquele IP** até os 15 minutos acabarem. Durante o bloqueio, a resposta é "muitas tentativas", **mesmo com a senha certa**. O dono da conta, vindo de outro IP, continua entrando. Vale também para e-mails sem conta, para o bloqueio não revelar se a conta existe. Um login certo antes do bloqueio zera a contagem.
- **Por IP:** no máximo **100 tentativas de login a cada 10 minutos**. É só um teto contra abuso, folgado porque, atrás do Firebase Hosting, vários usuários podem chegar com o mesmo IP.
- As senhas são guardadas com um cálculo propositalmente lento (custo 12), para dificultar quebrá-las se o banco vazar. Contas criadas com um custo menor passam para o atual no próximo login certo, sem o usuário perceber.

**Sessão**
- O acesso vale **15 minutos** e é renovado automaticamente enquanto o usuário usa o app.
- A renovação é **deslizante por 7 dias**: quem fica até 7 dias sem abrir o app continua logado.
- Toda sessão tem um **teto de 30 dias** a partir do login. Depois disso é preciso entrar de novo, mesmo com uso contínuo.
- Cada renovação troca a credencial por uma nova. Se uma credencial antiga for reapresentada, o sistema trata como roubo e **encerra todas as sessões** do usuário.
- Credencial expirada, revogada, reutilizada ou inexistente recebe sempre a mesma resposta, sem revelar o motivo.
- Cada sessão pode ser renovada no máximo **60 vezes a cada 10 minutos**. O acesso fica só na memória da aba, então todo recarregamento de página e toda aba nova fazem uma renovação; o limite folgado só barra abusos.

**Logout**
- Encerra a sessão no servidor. Pode ser repetido sem erro, mesmo se a sessão já tiver acabado.
- O acesso já emitido continua válido até expirar (no máximo 15 minutos).

**Minha conta**
- **Nome:** pode ser trocado a qualquer momento, com as mesmas regras do cadastro (obrigatório, até 100 caracteres, espaços nas pontas removidos). O e-mail não muda.
- **Trocar a senha:** pede a senha atual e a nova (8 a 50 caracteres). Senha atual errada não muda nada e é avisada antes de qualquer problema da nova. Ao salvar, **as outras sessões da conta são encerradas**, e a sessão de onde a troca foi feita continua aberta. O dono recebe um e-mail "Sua senha foi alterada" (no idioma da conta) com o link para escolher uma nova senha, caso não tenha sido ele; se o e-mail falhar, a troca vale do mesmo jeito.
- **Sair de todos os aparelhos:** encerra todas as sessões da conta, inclusive a atual, e volta para o login. O acesso já emitido em cada aparelho continua válido até expirar (no máximo 15 minutos).
- **Baixar meus dados:** um arquivo JSON (`lifemanager-aaaa-mm-dd.json`) com tudo o que é do usuário: conta (nome, e-mail, data de criação), preferências, categorias, meses, lançamentos, recorrências, orçamentos, objetivos de economia, titulares, o histórico das importações (sem as linhas de rascunhos em revisão), hábitos, check-ins, registros de meta, desafios, personagem, extrato do jogo, recompensas e resgates, itens comprados, conquistas e, dos grupos de ranking, só o nome de cada grupo e a data de entrada. **Nunca entram:** senha (nem cifrada), sessões, tentativas de login, a identificação das linhas do banco e qualquer dado de outros membros dos grupos. No máximo **3 downloads por hora**.
- **Excluir a conta:** pede a senha. A conta não some na hora: a exclusão fica marcada para **7 dias** depois, todas as sessões são encerradas e um e-mail, no idioma da conta, avisa a data e que entrar de novo até lá cancela. Pedir de novo durante o prazo não muda a data nem manda outro e-mail. Se o e-mail falhar, a exclusão continua marcada.
  - **Exclusão definitiva:** de hora em hora, as contas com o prazo vencido são apagadas com **todos os dados** (finanças, hábitos, jogo, importações, preferências, sessões). Antes, a pessoa sai de cada grupo de ranking pela regra de saída: num grupo de que era dona, o membro mais antigo vira o dono; um grupo em que estava sozinha é apagado. Não há como desfazer.
  - Os resultados das semanas já fechadas do ranking perdem a linha da pessoa (os pódios dos outros membros continuam).
  - **Durante o prazo** a conta funciona normalmente. **Entrar com a senha certa cancela a exclusão**, e o app avisa "a exclusão da sua conta foi cancelada". "Esqueceu a senha?" continua funcionando, para quem quiser voltar.
- **Limites por usuário:** no máximo **10 trocas de senha ou pedidos de exclusão a cada 10 minutos**, o que também barra quem tenta adivinhar a senha atual.

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
- Pelo app, **só é possível criar meses do ano corrente ou do anterior** (em janeiro, ainda dá para criar dezembro). Sem escolher o ano, vale o corrente. Meses mais antigos só aparecem quando já existiam, quando uma recorrência os abre ou quando uma [importação de extrato](#importação-de-extrato) é confirmada. Um [lançamento rápido](#lançamentos) também abre o mês que falta, com a mesma regra do ano.
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
- **Objetivo de economia:** um **investimento** pode ser ligado a um [objetivo](#objetivos-de-economia) ativo do usuário. Gastos e receitas não (`Transaction.GoalRequiresInvestment`). Ao editar, deixar o objetivo de fora desliga o lançamento dele. Um lançamento que já estava ligado a um objetivo que foi arquivado depois pode ser editado mantendo esse objetivo; só não dá para ligar um lançamento novo a um objetivo arquivado.
- **Lançamento rápido** ("Novo lançamento", sem escolher o mês antes): o lançamento cai no mês da sua data.
  - Se o usuário já tem esse mês, de qualquer ano, ele entra nele, como se fosse lançado dentro do mês.
  - Se o mês não existe, ele é **criado na hora**, mas só se for do ano corrente ou do anterior (a mesma regra da criação manual). Fora disso, o erro é o do ano (`MonthlySummary.YearNotCurrent`).
  - Tudo é conferido antes de criar o mês (categoria, descrição, valor): um lançamento recusado nunca deixa um mês vazio criado.
  - Conta para os [check-in automático](#check-in-automático-rotinas-financeiras) como qualquer lançamento digitado.
- **Exportar lançamentos (CSV):** baixa os lançamentos de um período numa planilha. É diferente de "Baixar meus dados" (Minha conta), que é o JSON completo da conta.
  - **Período:** as mesmas regras do [Painel](#painel): meses inteiros, fim não anterior ao início, no máximo **36 meses**, e os mesmos códigos de erro. Pode filtrar por tipo (todos, gastos, receitas ou investimentos).
  - **Colunas:** data, descrição, categoria, tipo, valor, mês (`aaaa-mm`) e origem. O **valor sai com sinal**: receita positiva, gasto e investimento negativos, então a soma da coluna dá o saldo do período.
  - **Origem:** "Recorrente" (lançado por uma recorrência), "Importação" (veio de um extrato, mesmo depois de a importação sair do histórico), "Resgate" (o gasto real de uma recompensa resgatada) ou "Manual". Um lançamento cuja recorrência foi excluída sai como "Manual".
  - **Formatos:** "português" (separador `;`, vírgula decimal, data `dd/mm/aaaa`, cabeçalhos em português; arquivo `lancamentos-aaaa-mm_aaaa-mm.csv`) ou "inglês" (separador `,`, ponto decimal, data `aaaa-mm-dd`, cabeçalhos em inglês; arquivo `transactions-aaaa-mm_aaaa-mm.csv`). O arquivo é UTF-8 com BOM, para o Excel ler os acentos.
  - **Ordem:** por data; no mesmo dia, na ordem em que foram criados.
  - **Segurança:** uma descrição ou categoria que começa com `=`, `+`, `-`, `@` ou tab ganha um `'` na frente, para a planilha não a executar como fórmula.
  - Um período sem lançamentos gera só o cabeçalho; o app avisa e não salva o arquivo. No máximo **10 exportações a cada 10 minutos**.

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
- **Transferência entre contas próprias** (Pix, TED ou transferência, nos dois sentidos, com o **nome do cadastro** do usuário ou um dos seus **titulares** na linha; veja "Titulares das minhas contas" abaixo): vem **fora da importação**. Todas as palavras do nome do cadastro precisam aparecer, na mesma ordem. Com 3 palavras ou mais, o banco pode ter nomes a mais no meio; com 2, elas precisam estar juntas ("Maria Silva" não reconhece "Maria Aparecida Silva", que é outra pessoa). Palavras de até 2 letras e "de", "da", "do", "das", "dos", "e" não contam. Com um nome de uma palavra só, nada é reconhecido, para não pegar outras pessoas com o mesmo nome.
- Só vale o **começo** do texto: "Pix enviado: Aplicação…" não é uma aplicação. O sentido do dinheiro decide: uma aplicação com valor de entrada é um estorno, logo um resgate.
- Incluir uma dessas linhas (por exemplo, o salário que chega de uma conta própria que não é importada) ensina a regra, e as próximas importações seguem essa decisão.

**Titulares das minhas contas** (os nomes que o banco escreve nas transferências entre contas próprias)
- O nome do cadastro é o nome de exibição e pode não ser o que o banco escreve ("Rafa" no app, "RAFAEL MOREIRA FERREIRA" no extrato). Por isso o usuário pode cadastrar **até 5 titulares**: o próprio nome completo, uma conta PJ, uma conta conjunta.
- Cada titular tem **até 100 caracteres** (espaços nas pontas são removidos) e precisa de **ao menos 2 palavras de nome**, contadas como no reconhecimento: palavras de até 2 letras e "de", "da", "do", "das", "dos", "e" não contam ("Rafael" e "Ana de Sá" não valem).
- O mesmo nome não pode aparecer duas vezes (ignorando acentos, maiúsculas e espaços).
- Salvar **substitui a lista inteira**. Uma lista vazia apaga todos.
- O reconhecimento procura o **nome do cadastro e todos os titulares**, cada um com a regra das palavras descrita acima. O que o usuário já decidiu para o estabelecimento continua valendo antes.
- A lista vale para os **próximos envios**: mudar os titulares não refaz as importações que já estão em revisão.

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

### Objetivos de economia

Um objetivo junta dinheiro para algo de médio prazo ("Viagem: R$ 5.000 até dez/2027", "Reserva de emergência: R$ 20.000"). Ele cresce com os **lançamentos de investimento ligados a ele**.

- **Nome:** obrigatório, até 60 caracteres, único entre os objetivos **ativos** do usuário (ignorando acentos e maiúsculas). Arquivar libera o nome.
- **Meta:** maior que zero, até 2 casas decimais (as regras de dinheiro dos lançamentos).
- **Prazo (opcional):** um mês. **Não pode ser anterior ao mês corrente**, nem na criação nem na edição. O mês corrente vale.
- **Já guardado:** o que existia antes do objetivo; zero ou mais, até 2 casas.
- **Ícone (opcional):** um dos ícones que o app conhece.
- **Guardado** = já guardado + a soma dos investimentos ligados. É calculado sempre a partir dos lançamentos, então editar ou excluir um lançamento muda o guardado na hora.
- **Falta** = meta − guardado (nunca negativo). **Progresso** = guardado ÷ meta (pode passar de 100%).
- **Meses restantes:** do mês corrente até o prazo, contando os dois.
- **Guardar por mês** = falta ÷ meses restantes, **arredondado para cima no centavo**. Com o prazo neste mês, ou já vencido, é tudo o que falta.
- **Ritmo:**
  - **Concluído:** o guardado chegou à meta.
  - **Sem prazo:** sem prazo, não há ritmo.
  - **No ritmo:** a média guardada nos **últimos 3 meses** (o corrente e os 2 anteriores, sempre dividindo por 3) cobre o "guardar por mês".
  - **Atrasado:** não cobre.
- **Concluído em:** a data em que o guardado chegou à meta pela primeira vez. É gravada quando um lançamento ligado é criado ou editado, ou quando o objetivo é criado ou editado (uma meta menor ou um "já guardado" maior podem concluí-lo). **Nunca volta:** se depois um lançamento for excluído, o objetivo continua em "Concluídos", mas o ritmo deixa de ser "concluído". Um objetivo **arquivado** nunca é concluído, nem pela edição de um lançamento que ainda aponta para ele.
- **Listas:** "Ativos" (sem data de conclusão), "Concluídos" (com data de conclusão) e "Arquivados". Busca pelo nome; ordena por nome, prazo (sem prazo por último na ordem crescente) ou progresso.
- **Detalhe:** o guardado em cada um dos **últimos 12 meses** (o corrente por último) e os lançamentos ligados, de todos os meses, do mais novo para o mais antigo.
- **Excluir arquiva:** os lançamentos continuam ligados e o objetivo pode ser restaurado, se nenhum objetivo ativo tiver pegado o nome. Um objetivo arquivado não pode ser editado nem receber lançamentos novos.
- Usar o dinheiro (pagar a viagem, por exemplo) **não desconta** do objetivo: ele mostra o quanto foi guardado. Para tirar, edite ou exclua o lançamento de investimento.
- Objetivos não são ligados na importação de extrato.

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
- **Economia com hábitos:** soma, no período, os dias limpos × o custo por recaída de cada hábito para evitar que tem custo (ver [Economia](#economia-hábitos-para-evitar)). Não tem comparação com o período anterior.
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
- **Meta diária:** opcional, só em hábitos para construir. Veja [Meta diária](#meta-diária-hábitos-mensuráveis).

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

### Check-in automático (rotinas financeiras)

Um hábito para construir **sem meta diária** pode ser marcado sozinho quando o usuário faz uma ação no módulo financeiro ("Registrar meus gastos", "Conciliar o extrato", "Revisar o orçamento"). Cada hábito tem no máximo um **gatilho**:

- **Lançar uma transação** à mão. Lançamentos de recorrências, de importações e despesas de resgate de recompensa **não** contam.
- **Confirmar uma importação de extrato** (o que ela lança não conta como "lançar uma transação").
- **Salvar o orçamento** de um mês.

Regras:

- O hábito é marcado **hoje**, qualquer que seja a data da transação ou o mês do orçamento, e só se estiver agendado hoje, a partir da data de início, sem marcação hoje e, no hábito "vezes por semana", com a meta da semana ainda aberta.
- Vale **um check-in normal**: mesma recompensa, ofensiva, marco e proteção. A segunda ação do dia não paga de novo.
- O hábito continua podendo ser marcado à mão.
- A ação financeira **nunca falha** por causa do hábito: se o check-in automático der erro, a transação, a importação ou o orçamento são salvos do mesmo jeito.
- Apagar a transação ou desfazer a importação **não** desmarca o hábito. O usuário pode desfazer o check-in à mão, e a próxima ação do dia marca de novo.
- Hábitos para evitar e hábitos com meta diária não podem ter gatilho.

### Meta diária (hábitos mensuráveis)

Um hábito para construir pode ter uma **meta diária** de quantidade ("2.000 ml de água", "20 páginas"). Em vez de marcar o dia com um toque, o usuário **registra o progresso** ao longo do dia.

- **A meta tem três partes:**
  - **valor:** número inteiro de 1 a 100.000;
  - **unidade:** obrigatória, até 15 caracteres (ex.: "ml", "páginas", "min");
  - **incremento:** opcional, de 1 até o valor da meta (o botão rápido "+250").
- Só números inteiros: quem precisa de fração escolhe uma unidade menor ("m" em vez de "km").
- Hábitos para evitar **não** têm meta.
- **Registro de progresso:** um valor de 1 a 100.000, nas mesmas condições do check-in (hábito ativo, dia agendado, a partir do início, **só hoje ou ontem**). No máximo **50 registros por dia**.
- **Bater a meta:** quando a soma do dia chega à meta, o dia recebe o **check-in normal**, com a mesma recompensa, ofensiva, marco e proteção. A regra do hábito semanal vale igual: depois da meta da semana, o dia conta mas não rende.
- **Passar da meta** não rende nada a mais: o registro é guardado, mas o dia já foi pago.
- **Remover um registro** (hoje ou ontem): se a soma cair abaixo da meta, o check-in do dia é **desfeito** como no desfazer normal (devolve tudo; o HP para em 1).
- Um hábito com meta **não aceita o check-in de um toque**. **Desfazer o check-in** dele apaga também os registros do dia, e o dia recomeça do zero.
- **Progresso parcial não vale nada:** no fechamento do dia, um dia abaixo da meta é cobrado como **falha** (ou usa uma proteção), igual a um dia sem nada. Os registros ficam guardados.
- **Mudar a meta** (inclusive tirar ou pôr) não conclui nem desfaz nenhum dia sozinho: a meta nova vale a partir do próximo registro ou remoção. Os dias já concluídos ficam como estão.
  - Um hábito que vira mensurável com o dia de hoje já feito continua feito. Remover um registro com a soma abaixo da meta desfaz esse dia.
  - Um hábito que volta a ser de um toque mantém os registros antigos, que passam a ser ignorados.

### Recaídas (hábitos para evitar)

Hábitos para evitar não recebem check-in. **Cada dia sem recaída vira um "dia limpo"** quando o dia fecha.

- A recaída só pode ser registrada em hábito ativo, para evitar, num dia em que ele é evitado (não num dia liberado), **hoje ou ontem**, a partir da data de início.
- Uma recaída por hábito por dia.
- **Custo:** o HP da dificuldade. No limite semanal, as primeiras N recaídas da semana são grátis e só as seguintes custam HP.
- A recaída **zera a ofensiva na hora**.
- **Desfazer** devolve o HP perdido. Se a recaída causou um nocaute, as moedas perdidas no nocaute não voltam.

### Economia (hábitos para evitar)

Um hábito para evitar pode ter um **custo por recaída** em dinheiro ("um delivery: R$ 45"). Só hábitos para evitar têm custo; ele é opcional, positivo, com até 2 casas decimais, e pode ser mudado ou removido a qualquer momento.

- **Economia = dias limpos × custo.** Vale para qualquer frequência: no limite semanal, cada dia sem recaída de uma semana dentro do limite já é um dia limpo.
- Os dias limpos só existem depois do fechamento do dia, então a economia de hoje e de ontem aparece depois.
- Mudar o custo **recalcula todo o histórico** com o novo valor.
- A economia é só informativa: não cria lançamento, não mexe em moedas e não registra se foi investida. O botão "Investir" do painel abre um lançamento de investimento já preenchido, que o usuário confere e salva.
- Aparece no detalhe do hábito (economia desde o início) e no **painel** (economia do período, por hábito, do que mais economizou para o que menos). Hábitos arquivados continuam contando.

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
- **Mapa das últimas 13 semanas** (91 dias). Cada dia tem um estado: antes do início, folga, feito, dia limpo, protegido, falhou, recaída, pendente, sem registro ou **parcial**. Uma falha nunca é deduzida: só aparece o que foi registrado.
- **Dia parcial:** um dia com progresso registrado, mas sem check-in, aparece como "parcial" em vez de "falhou", "pendente" ou "sem registro" (também hoje em andamento). Um dia protegido continua "protegido". É só a forma de mostrar: a consistência e a cobrança não mudam.
- Cada dia com registros traz a **quantidade do dia**, comparada com a meta atual do hábito (a meta não é guardada por dia).
- **Consistência:**
  - Hábitos diários e de dias fixos: parte dos dias agendados dos últimos 30 dias que foram cumpridos (dias protegidos ficam fora da conta).
  - Hábitos semanais: parte das últimas 4 semanas fechadas que bateram a meta ou ficaram dentro do limite.
- **Economia:** num hábito para evitar com custo por recaída, os dias limpos desde o início × o custo.
- **Rumo ao automático:** total de dias cumpridos, rumo aos 66 dias (média para um hábito virar automático). Falhar um dia não zera esse total.
- Hábitos arquivados também têm estatísticas.

**Extrato** (histórico)
- Lista toda mudança em moedas, XP e HP, da mais recente para a mais antiga: hábito cumprido, falha, recaída, dia limpo, marco, resgate, item comprado, conquista, nocaute, desfeito, proteção usada e subida de nível.
- Pode ser filtrado por hábito.

**Tela "Hoje"**
- Mostra os hábitos para construir agendados para hoje e os de **ontem que ficaram pendentes**. Um hábito semanal só aparece como pendente enquanto a meta da semana não foi batida.
- Mostra os hábitos para evitar em vigor hoje e os que estão **liberados** hoje.
- Se houve alguma falha nos últimos 7 dias, o app pode mostrar uma mensagem de boas-vindas de volta.

### Conquistas

São **31 conquistas fixas**, em três níveis: **bronze, prata e ouro**. Cada uma é ganha **uma vez só**, paga um **bônus único** e libera um **título**.

| Família | Bronze | Prata | Ouro |
|---|---|---|---|
| **Check-ins** (hábitos para construir feitos) | 10 | 100 | 500 |
| **Ofensiva** (maior recorde de algum hábito, em dias) | 7 | 30 | 100 |
| **Nível** | 5 | 10 | 25 |
| **Resgates** na loja | 1 | 10 | — |
| **Dias limpos** (hábitos para evitar) | 10 | 50 | 200 |
| **Volta por cima**: dias cumpridos depois do primeiro nocaute | — | 10 | — |
| **Piloto automático**: dias cumpridos de um mesmo hábito | — | — | 66 |
| **Ranking: vitórias** (semanas em 1º num grupo) | 1 | 5 | 20 |
| **Ranking: pódios** (semanas entre os 3 primeiros) | 3 | 10 | 30 |
| **Finanças: orçamento** (meses dentro do limite de gastos) | 1 | 3 | 6 |
| **Finanças: investimento** (meses que bateram a fatia de investimentos) | 2 | 6 | 12 |
| **Finanças: poupança** (meses que pouparam 20% das receitas) | 1 | 3 | 12 |

**Bônus por nível**

| Nível | Moedas | XP |
|---|---|---|
| Bronze | 25 | 50 |
| Prata | 100 | 150 |
| Ouro | 300 | 400 |

- **Como conta:**
  - vale o **histórico inteiro**, incluindo hábitos arquivados;
  - "dias cumpridos" são os feitos e os dias limpos, a mesma conta do "rumo ao automático";
  - nos hábitos semanais, cada semana de ofensiva vale 7 dias;
  - um check-in desfeito ou um resgate desfeito deixa de contar;
  - compras de itens de jogo não contam como resgate;
  - **ranking:** só vale uma semana **fechada** de um grupo com **3 ou mais membros desde segunda**, em que o jogador também estava desde segunda e fez pontos. Empate em 1º vale vitória para todos os empatados. Contam **semanas diferentes**: vencer a mesma semana em dois grupos conta uma vez só.
  - **finanças:** só contam **meses fechados** (o mês em andamento nunca conta).
    - **Orçamento e investimento** exigem um orçamento do mês **criado até o dia 5** desse mês (pode ser antes do mês começar) e **não alterado depois que o mês acabou**. Assim não dá para criar ou afrouxar a meta depois de saber o resultado.
    - **Orçamento:** os gastos ficaram dentro do limite de gastos (total menos a fatia de investimentos) **e** chegaram a pelo menos **metade** dele. Um total inflado para passar fácil não conta.
    - **Investimento:** o investido no mês chegou à fatia de investimentos. Um orçamento sem essa fatia não conta.
    - **Poupança:** receitas − gastos ≥ **20% das receitas** (investir conta como poupar, como no painel). Não precisa de orçamento; um mês sem receitas não conta.
    - Os orçamentos salvos antes desta regra existir contam como criados no início do próprio mês.
- **Para sempre:** desfazer o check-in, arquivar o hábito ou perder XP **não** tira a conquista nem o bônus.
- **Bônus:** entra no extrato como "conquista". Se o XP do bônus subir o nível, a subida vem com HP cheio, como sempre, e pode liberar uma conquista de nível na mesma hora.
- **Quando é ganha:** sempre que o perfil do jogador é lido, o que o app faz ao abrir e depois de toda mudança. Uma conquista que o fechamento do dia liberou de madrugada aparece quando o jogador abre o app, com a data desse dia.
- **Aviso:** cada conquista nova aparece uma vez ("Conquista desbloqueada"), em qualquer aparelho.
- O bônus de conquista **não** entra no ritmo de ganho da loja.

### Perfil do jogador

A página de perfil mostra quem o jogador é no jogo e o caminho que já fez.

- **Aparência:**
  - **avatar:** um de 12 ícones fixos, todos liberados (o padrão é o gato);
  - **título:** o de qualquer conquista **desbloqueada**, ou nenhum. Como a conquista nunca é perdida, o título escolhido vale para sempre;
  - trocar é de graça, sem limite, e não entra no extrato.
- **Estatísticas** (o histórico inteiro, hábitos arquivados incluídos; as que existem nas conquistas usam a mesma conta):
  - check-ins feitos, dias limpos, melhor ofensiva (semana = 7 dias) e mais dias cumpridos de um hábito;
  - dias ativos (dias com algum dia cumprido), hábitos criados e resgates não desfeitos;
  - **moedas ganhas:** com hábitos, pela regra do ritmo de ganho (um check-in desfeito devolve as suas), mais os bônus de conquista;
  - **moedas gastas:** resgates e itens de jogo, sem os que foram desfeitos;
  - nocautes, conquistas desbloqueadas e "jogando desde" (o dia do primeiro lançamento do extrato).
- **Evolução:** o XP total e o saldo de moedas ao fim de cada dia de jogo, nos últimos 30, 90 (padrão) ou 365 dias, até hoje. Sai do extrato: cada dia soma tudo o que veio antes, então o último ponto é o perfil de agora, e um dia sem movimento repete o anterior.

### Desafios da semana

Um desafio é uma meta **semanal** num hábito para construir: fazer **mais dias** (frequência) ou **mais quantidade** (volume, só para hábitos com meta diária) do que o normal. É opcional: cumprir paga um bônus, não cumprir **não custa nada**.

- **Semanas:** a atual aceita desafios **até quarta-feira**; a próxima pode ser planejada a qualquer momento. No máximo **3 desafios por semana** e **um por hábito**.
- **Hábitos aceitos:** ativos, para construir, já iniciados até o domingo da semana. Hábitos para evitar não têm desafio.
- **Meta:** acima do que já foi feito na semana. Frequência: até os dias possíveis da semana (numa semana em andamento, só os dias feitos ou ainda abertos a check-in). Volume: até 3 semanas da meta diária (7 × meta × 3).
- **Média (base):** a média semanal das até **4 últimas semanas fechadas**, só semanas **inteiras** desde o início do hábito (a semana em que ele começou no meio fica de fora, para não puxar a média para baixo); sem nenhuma, não há média.
- **Esforço**, comparando a meta com a média:
  - **Leve:** meta até a média, ou qualquer meta quando ainda não há média (assim um hábito recém-criado não rende o bônus maior nem pontos no ranking com uma meta de 1 dia);
  - **Firme:** até 1 dia a mais (frequência) ou até 25% a mais (volume);
  - **Puxado:** acima disso.
- **Recompensa:** a recompensa de um check-in do hábito (pela dificuldade) × **2** (Leve), **×4** (Firme) ou **×6** (Puxado), em moedas e XP. O formulário mostra o esforço e a recompensa antes de criar.
- **Progresso:** sempre recontado do que está gravado: dias **feitos** na semana (dia protegido não conta) ou a soma das quantidades registradas na semana (dias abaixo da meta diária também somam).
- **Conclusão:** ao alcançar a meta, o desafio é concluído e paga **uma vez** (lançamento "Desafio concluído" no extrato), com uma celebração na próxima visita. Desfazer o check-in depois **não** desfaz o bônus.
- **Falha:** a partir de terça, quando o domingo não pode mais mudar, um desafio não alcançado vira "não deu", sem custo.
- **Histórico:** um desafio concluído ou que não deu guarda o progresso com que terminou; desfazer um check-in depois não muda o que o histórico mostra.
- **Desistir:** um desafio ativo pode ser apagado; não paga nem conta para nada. Arquivar o hábito cancela os desafios ativos dele.
- **Sugestões:** para cada hábito com semana fechada, um pouco acima da média (1 dia a mais, ou 15% a mais de quantidade, preferida quando há meta diária), começando pelos hábitos mais esquecidos e depois os mais difíceis, até as vagas da semana. Aceitar cria o desafio com um toque.

### Resumo da semana

- Na **primeira visita da semana** aparece o resumo da semana anterior: hábitos cumpridos, dias limpos, moedas ganhas (hábitos, já descontados os desfeitos, mais os bônus de desafios) e desafios concluídos, cada número comparado com a semana antes dela; como terminou cada desafio; e a posição em cada grupo de ranking.
- Aparece **uma vez**, em qualquer aparelho. Quem não jogou na semana (nenhum hábito cumprido, dia limpo ou desafio) não vê resumo; estar num grupo não muda isso.
- **Ranking no resumo:** os grupos em que o jogador está agora, criados até aquela semana: a posição gravada se a semana já fechou, senão a calculada na hora, marcada **provisória** (na segunda, normalmente). Tocar no grupo abre o ranking dele.
- Na segunda, o domingo ainda aceita check-in: se ele concluir um desafio, o desafio paga e é celebrado normalmente, mesmo com o resumo já visto.
- O atalho **Planejar a semana** leva aos desafios.

## Loja de recompensas

O próprio usuário cadastra as recompensas e as compra com as moedas ganhas nos hábitos.

- **Nome:** obrigatório, até 60 caracteres, único entre as recompensas **ativas**.
- **Preço:** de 1 a 100.000 moedas (número inteiro). Mudar o preço vale só para os próximos resgates.
- **Ícone:** opcional, escolhido de uma lista fixa.
- **Excluir = arquivar:** a recompensa sai da loja, mas os resgates continuam no histórico. Uma recompensa arquivada não pode ser editada nem resgatada. **Restaurar** é recusado se já existir uma ativa com o mesmo nome.
- **Resgatar** desconta o preço das moedas. O saldo é conferido no momento do resgate: sem moedas suficientes, nada acontece.
- O resgate guarda o nome, o ícone e o **preço pago**. Mudar a recompensa depois não altera o histórico.
- **Desfazer um resgate:** só **no mesmo dia** do resgate e só uma vez. O preço pago volta integralmente.
- **Gasto em dinheiro (opcional):** uma recompensa pode ter uma **categoria** e um **valor em R$** (as duas coisas juntas, ou nenhuma; o valor segue as regras de valor de uma transação e a categoria tem que ser do usuário). É o custo real do prêmio ("Jantar fora", R$ 120 em Lazer).
  - Ao resgatar, o usuário escolhe se **lança o gasto**. Se sim, o resgate cria uma **despesa de hoje** com o nome da recompensa, a categoria e o valor, no mês de hoje (criado se ainda não existir), e os totais do mês são atualizados. Moedas e despesa entram juntas: sem moedas suficientes, nenhuma das duas acontece (e o mês de hoje não é criado).
  - Pedir para lançar o gasto de uma recompensa sem gasto configurado é recusado.
  - **Desfazer o resgate apaga a despesa** que ele lançou e atualiza o mês. Se o usuário já tinha apagado a despesa à mão, o resgate é desfeito normalmente.
  - Apagar a despesa à mão **não desfaz o resgate**: as moedas continuam gastas.
  - Mudar o valor ou a categoria da recompensa vale só para os próximos resgates.
  - Apagar a categoria **remove o gasto da recompensa** (categoria e valor); a recompensa continua na loja, sem gasto.
- **Ritmo de ganho:** a loja mostra quantos dias de hábitos cada recompensa custa, com base na média de moedas por dia ganhas com hábitos nos **últimos 14 dias**. Resgates, compras de itens, bônus de conquistas e nocautes não entram nessa média.

### Itens de jogo

Além das recompensas do usuário, a loja vende dois **itens de jogo** fixos, que agem na hora da compra (não há inventário):

| Item | Preço | Efeito | Limite |
|---|---|---|---|
| **Poção de HP** | 40 moedas | +25 HP, sem passar de 100 | 3 por dia |
| **Proteção de ofensiva** | 150 moedas | +1 proteção, igual à ganha pela ofensiva | 1 por semana (segunda a domingo) |

- **Quando é recusada** (nada muda), nesta ordem: o limite do dia ou da semana já foi atingido; a poção com o HP cheio, ou a proteção com 2 guardadas; moedas insuficientes. Compras desfeitas não contam para o limite.
- **A poção custa sempre 40 moedas**, mesmo quando cura menos de 25 (HP perto do máximo). Ela não mexe em XP, então não sobe nível.
- **A proteção comprada é igual a uma ganha:** o fechamento do dia a usa da mesma forma, e o máximo continua sendo 2.
- A compra fica no **extrato** como "item comprado", com as moedas pagas e o HP curado.
- **Desfazer uma compra:** só **no mesmo dia** e só uma vez. O preço volta integralmente e o limite é liberado de novo.
  - **Poção:** a cura é retirada, mas o HP para em 1 (o desfazer nunca derruba o jogador).
  - **Proteção:** só se o jogador ainda tiver alguma guardada. Se o fechamento do dia já a usou, o desfazer é recusado. As proteções não guardam a origem: o desfazer tira uma das guardadas, seja a comprada ou uma ganha pela ofensiva. O saldo final é o mesmo de quem nunca comprou.

## Ranking em grupos

Grupos fechados de amigos, criados pelos próprios jogadores, comparam a **semana de hábitos** de cada um. Não há ranking global.

### Pontos da semana

A semana do ranking vai de **segunda a domingo**, a mesma da ofensiva semanal. Os pontos saem dos registros de cada dia, sem nada a mais para marcar:

- **10 pontos por hábito cumprido no dia** (feito, ou dia limpo de um hábito para evitar), contando **no máximo 5 hábitos por dia**. Ter mais hábitos não dá mais pontos.
- **+10 pontos de dia perfeito** quando, no dia, havia ao menos um hábito previsto, **todos** os hábitos para construir previstos foram feitos e **nenhum** hábito para evitar teve recaída.
  - um dia **protegido** (proteção de ofensiva) sai da conta: não ajuda nem atrapalha o dia perfeito, e não dá pontos;
  - hábitos **N vezes por semana** dão pontos quando feitos, mas ficam fora da conta do dia perfeito;
  - um hábito arquivado ainda conta no dia em que foi arquivado; antes do início, não conta.
- **+15 pontos por desafio** da semana **Firme ou Puxado** com a meta alcançada, até **3 desafios** (+45). Conta o progresso gravado, não o status: desfazer o check-in que bateu a meta tira os pontos (o bônus em moedas fica). Desafios Leves, que não deram ou foram cancelados não contam.
- **Máximo:** 60 pontos por dia, 420 por semana, mais os desafios.
- Os **dias limpos** só são gravados pelo fechamento do dia (até 2 dias depois), por isso os pontos de hábitos para evitar chegam com atraso: o placar pode subir na terça.

### Grupos

- **Criar:** nome de 1 a 40 caracteres; quem cria é o **dono** e o primeiro membro.
- **Limites:** cada jogador participa de **até 5 grupos**; cada grupo tem **até 20 membros**.
- **Convite:** cada grupo tem um **código de 8 caracteres** (sem letras e números que se confundem, como O/0 e I/1), compartilhado como código ou link. Quem tem o código vê o nome do grupo, o dono e quantos membros há antes de entrar. Só o dono pode **gerar um código novo**, e o antigo para de funcionar.
- **Dono:** só ele renomeia o grupo, gera código novo e remove membros. Ele não pode se remover: para isso, **sai** do grupo.
- **Sair:** qualquer membro pode sair. Se o dono sai, o membro **mais antigo** vira dono. Se o último membro sai, o grupo é **apagado**. Quem saiu ou foi removido pode voltar pelo convite.
- **Privacidade:** quem não é membro não sabe nem se o grupo existe ("não encontrado"). Dentro do grupo, os membros veem só nome, avatar, título e os pontos da semana.
- O código do convite tem um limite de **30 tentativas a cada 10 minutos por jogador** (prévia e entrada).
- Se o dono sai do grupo enquanto renomeia, gera código novo ou remove alguém, o pedido é recusado ("só o dono"). Renomear nunca desfaz um código novo gerado ao mesmo tempo.

### Ranking do grupo

- **Posição:** por pontos; empate nos pontos desempata por dias perfeitos e depois por hábitos cumpridos. Empate nos três divide a posição (1º, 1º, 3º); na tela, os empatados aparecem por nome.
- **Estado da semana:**
  - **ao vivo:** a semana atual, calculada na hora para os membros de agora;
  - **fechando:** a semana acabou, mas ainda não foi gravada (os pontos ainda podem mudar com o fechamento do dia);
  - **final:** gravada. Um resultado final **nunca muda**: desfazer um check-in ou sair do grupo não reescreve a semana. Quem saiu continua aparecendo nela, marcado como "saiu do grupo".
- **Fechamento da semana:** feito de hora em hora, a partir de terça. A semana só fecha quando o fechamento do dia de **todos** os membros já julgou o domingo. Se algum ainda estiver atrasado **3 dias depois da terça**, ela fecha mesmo assim com o que está gravado. Entram os membros do momento do fechamento que já estavam no grupo até o domingo. A primeira semana do grupo é a da sua criação.
- **Quem vale para conquistas:** só quem estava no grupo **desde a segunda** da semana. Quem entrou no meio aparece no ranking, mas a semana não lhe dá conquista; e a semana só vale conquistas com **3 ou mais** desses membros (veja [Conquistas](#conquistas)).
- **Avisos:** na semana atual (menos na segunda), o jogador vê quem **passou à sua frente desde ontem** (quem estava atrás ou empatado ontem e hoje está à frente) e quantos pontos faltam para alcançar quem está logo à frente.
- **Semana passada:** junto com a semana atual vem o **pódio** da semana anterior (até o 3º lugar, com empates) e a posição do jogador nela.
- **Navegação:** da semana atual até a primeira semana do grupo. Semanas futuras ou anteriores à criação do grupo não existem.
- Na lista de grupos, cada um mostra a posição e os pontos do jogador na semana atual.

### Reações

- Um membro pode reagir à semana de outro com **👏** ou **🔥**: **uma reação por pessoa, por membro e por semana** em cada grupo. Reagir de novo troca a reação; dá para desfazer.
- Só na **semana atual e na anterior**, e nunca à própria semana.
- Cada linha do ranking mostra quantas reações de cada tipo o membro recebeu e qual eu dei. **Quem reagiu** só aparece para quem recebeu.
- Quem sai do grupo leva junto as reações que deu e recebeu nele.

## Apêndice: códigos de erro

A API responde a falhas esperadas com `{ code, message, type }`. O frontend usa o `code` para mostrar a mensagem traduzida. Quando o envio é uma lista e um item dela falha (os titulares), a resposta também traz `index`: a posição do item, a partir de 0. **Os códigos fazem parte do contrato e não devem ser renomeados.**

O `type` define o status HTTP: `Validation` → 400, `Unauthorized` → 401, `NotFound` → 404, `Conflict` → 409, `TooManyRequests` → 429.

### Contas

| Código | Tipo | Quando acontece |
|---|---|---|
| `User.UserNameIsNullOrWhiteSpace` | Validation | Nome vazio no cadastro ou em Minha conta |
| `User.UserNameTooLong` | Validation | Nome com mais de 100 caracteres |
| `User.EmailIsNullOrWhiteSpace` | Validation | E-mail vazio |
| `User.EmailIsInvalid` | Validation | E-mail fora do formato |
| `User.PlainPasswordIsNullOrWhiteSpace` | Validation | Senha vazia |
| `User.PlainPasswordTooShort` | Validation | Senha com menos de 8 caracteres |
| `User.PlainPasswordTooLong` | Validation | Senha com mais de 50 caracteres |
| `User.WrongPassword` | Validation | Senha atual errada em Minha conta (trocar a senha, excluir a conta) |
| `User.InvalidCredentials` | Unauthorized | E-mail ou senha errados |
| `User.NotFound` | NotFound | Usuário não existe |
| `Auth.InvalidRefreshToken` | Unauthorized | Sessão expirada, revogada ou inválida (é preciso entrar de novo) |
| `Auth.InvalidEmailToken` | Validation | Link do e-mail (cadastro ou recuperação) vencido, já usado ou desconhecido |
| `Auth.TooManyRequests` | TooManyRequests (429) | Muitos pedidos de cadastro/recuperação, usos de link ou logins do mesmo IP em 10 minutos; e-mail bloqueado naquele IP por 5 senhas erradas em 15 minutos; muitas renovações da mesma sessão em 10 minutos; tentativas de convite do mesmo jogador; ou mais de 10 exportações de lançamentos em 10 minutos |
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
| `MonthlySummary.YearNotCurrent` | Validation | Criação de mês fora do ano corrente e do anterior ("O ano deve ser o corrente ou o anterior") |
| `MonthlySummary.AlreadyExists` | Conflict | O mês já foi criado |
| `MonthlySummary.NotFound` | NotFound | Mês não existe ou é de outro usuário |
| `Transaction.InvalidType` | Validation | Tipo diferente de gasto, receita ou investimento |
| `Transaction.DescriptionIsNullOrWhiteSpace` | Validation | Descrição vazia |
| `Transaction.DescriptionTooLong` | Validation | Descrição com mais de 80 caracteres |
| `Transaction.AmountNotPositive` | Validation | Valor zero ou negativo |
| `Transaction.AmountTooManyDecimals` | Validation | Mais de 2 casas decimais |
| `Transaction.AmountTooLarge` | Validation | Valor acima do máximo |
| `Transaction.GoalRequiresInvestment` | Validation | Objetivo de economia num lançamento que não é investimento |
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
| `AccountHolder.NamesRequired` | Validation | Titulares salvos sem a lista de nomes |
| `AccountHolder.TooMany` | Validation | Mais de 5 titulares |
| `AccountHolder.NameIsNullOrWhiteSpace` | Validation | Titular vazio (com `index`) |
| `AccountHolder.NameTooLong` | Validation | Titular com mais de 100 caracteres (com `index`) |
| `AccountHolder.NameTooShort` | Validation | Titular com menos de 2 palavras de nome (com `index`) |
| `AccountHolder.DuplicateName` | Validation | O mesmo titular duas vezes; o `index` é o da segunda vez |
| `BudgetPlan.InvalidMonth` | Validation | Mês em formato inválido |
| `BudgetPlan.InvalidType` | Validation | Fatia que não é de gasto nem de investimento |
| `BudgetPlan.TotalNotPositive` / `TotalTooManyDecimals` / `TotalTooLarge` | Validation | Total do orçamento inválido (mesmas regras dos lançamentos) |
| `BudgetPlan.PercentOutOfRange` / `PercentTooManyDecimals` | Validation | % de uma fatia fora de 0 (exclusivo) a 100, ou com mais de 2 casas |
| `BudgetPlan.OverAllocated` | Validation | As fatias somam mais de 100% |
| `BudgetPlan.DuplicateCategory` | Validation | A mesma categoria em duas fatias |
| `BudgetPlan.ExpenseSliceWithoutCategory` | Validation | Fatia de gasto sem categoria |
| `BudgetPlan.InvalidInvestmentSlice` | Validation | Fatia de investimento com categoria, ou mais de uma |
| `BudgetPlan.NotFound` | NotFound | O mês não tem orçamento para excluir |
| `SavingsGoal.NameIsNullOrWhiteSpace` | Validation | Nome do objetivo vazio |
| `SavingsGoal.NameTooLong` | Validation | Nome com mais de 60 caracteres |
| `SavingsGoal.InvalidTargetAmount` | Validation | Meta zero, negativa, com mais de 2 casas ou acima do máximo |
| `SavingsGoal.InvalidInitialAmount` | Validation | "Já guardado" negativo, com mais de 2 casas ou acima do máximo |
| `SavingsGoal.InvalidTargetMonth` | Validation | Prazo em formato inválido (não é `aaaa-mm`) |
| `SavingsGoal.TargetMonthInPast` | Validation | Prazo antes do mês corrente |
| `SavingsGoal.IconTooLong` | Validation | Ícone inválido |
| `SavingsGoal.NameAlreadyExists` | Conflict | Já existe objetivo **ativo** com esse nome (ao criar, renomear ou restaurar) |
| `SavingsGoal.Archived` | Conflict | Edição de objetivo arquivado, ou lançamento ligado a um objetivo arquivado |
| `SavingsGoal.AlreadyArchived` / `NotArchived` | Conflict | Arquivar um objetivo já arquivado, ou restaurar um que não está arquivado |
| `SavingsGoal.NotFound` | NotFound | Objetivo não existe ou é de outro usuário (também ao ligar um lançamento) |
| `FinanceDashboard.InvalidFrom` / `InvalidTo` | Validation | Mês do período (painel ou exportação de lançamentos) em formato inválido |
| `FinanceDashboard.EndBeforeStart` | Validation | Fim do período antes do início (painel ou exportação de lançamentos) |
| `FinanceDashboard.PeriodTooLong` | Validation | Período maior que 36 meses (painel ou exportação de lançamentos) |
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
| `Habit.InvalidTargetValue` | Validation | Meta diária fora de 1 a 100.000 |
| `Habit.TargetUnitRequired` | Validation | Meta diária sem unidade |
| `Habit.TargetUnitTooLong` | Validation | Unidade com mais de 15 caracteres |
| `Habit.InvalidTargetStep` | Validation | Incremento fora de 1 até o valor da meta |
| `Habit.InvalidTargetCombination` | Validation | Unidade ou incremento informados sem meta |
| `Habit.TargetNotAllowed` | Validation | Meta diária num hábito para evitar |
| `Habit.RelapseCostNotPositive` | Validation | Custo por recaída zero ou negativo |
| `Habit.RelapseCostTooManyDecimals` | Validation | Custo por recaída com mais de 2 casas decimais |
| `Habit.RelapseCostTooLarge` | Validation | Custo por recaída grande demais |
| `Habit.RelapseCostNotAllowed` | Validation | Custo por recaída num hábito para construir |
| `Habit.InvalidAutoTrigger` | Validation | Gatilho de check-in automático inválido |
| `Habit.AutoTriggerNotAllowed` | Validation | Gatilho de check-in automático num hábito para evitar ou com meta diária |
| `Habit.NotMeasurable` | Validation | Registro de progresso num hábito sem meta |
| `Habit.InvalidProgressValue` | Validation | Registro de progresso fora de 1 a 100.000 |
| `Habit.TooManyProgressEntries` | Validation | Mais de 50 registros de progresso no dia |
| `Habit.ProgressRequired` | Validation | Check-in de um toque num hábito com meta |
| `Habit.ProgressEntryNotFound` | NotFound | Remover um registro de progresso que não existe (ou de outro hábito) |
| `PlayerProfile.StreakFreezesFull` | Conflict | O jogador já tem o máximo de proteções |
| `PlayerProfile.NoStreakFreeze` | Conflict | Uso de proteção sem ter nenhuma |
| `PlayerProfile.UnknownAvatar` | Validation | Avatar fora da lista fixa |
| `PlayerProfile.TitleLocked` | Validation | Título de uma conquista não desbloqueada |
| `PlayerProfile.UnknownEvolutionRange` | Validation | Período da evolução que não é `Month`, `Quarter` nem `Year` |

### Desafios

| Código | Tipo | Quando acontece |
|---|---|---|
| `Challenge.NotFound` | NotFound | Desafio que não existe ou é de outro jogador |
| `Challenge.InvalidType` | Validation | Tipo desconhecido |
| `Challenge.InvalidWeek` | Validation | Semana diferente da atual ou da próxima |
| `Challenge.InvalidSummaryWeek` | Validation | Marcar como visto um resumo cuja semana não é uma segunda anterior à atual |
| `Challenge.TooManyIds` | Validation | Mais de 100 desafios marcados como vistos de uma vez |
| `Challenge.CreationWindowClosed` | Validation | Desafio para a semana atual depois de quarta-feira |
| `Challenge.HabitNotEligible` | Validation | Hábito arquivado, para evitar ou que só começa depois da semana |
| `Challenge.NotMeasurable` | Validation | Desafio de volume num hábito sem meta diária |
| `Challenge.InvalidTarget` | Validation | Meta fora do limite da semana |
| `Challenge.TargetAlreadyReached` | Validation | Meta que não passa do já feito na semana |
| `Challenge.AlreadyExists` | Conflict | O hábito já tem desafio na semana |
| `Challenge.WeekLimitReached` | Conflict | A semana já tem 3 desafios |
| `Challenge.NotActive` | Conflict | Desistir de um desafio já concluído, que não deu ou cancelado |

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
| `Rewards.ExpenseIncomplete` | Validation | Gasto da recompensa com categoria sem valor, ou valor sem categoria |
| `Rewards.ExpenseNotConfigured` | Validation | Pedido para lançar o gasto de uma recompensa que não tem gasto |
| `GameItems.UnknownItem` | Validation | Item que não existe na loja |
| `GameItems.InsufficientCoins` | Conflict | Moedas insuficientes para o item |
| `GameItems.HpFull` | Conflict | Poção com o HP já no máximo |
| `GameItems.StreakFreezesFull` | Conflict | Proteção com 2 já guardadas |
| `GameItems.DailyLimitReached` | Conflict | 4ª poção no mesmo dia |
| `GameItems.WeeklyLimitReached` | Conflict | 2ª proteção na mesma semana |
| `GameItems.UndoOutsideWindow` | Validation | Desfazer uma compra que não é de hoje |
| `GameItems.AlreadyUndone` | Conflict | A compra já foi desfeita |
| `GameItems.ItemAlreadyUsed` | Conflict | Desfazer uma proteção que o fechamento do dia já usou |
| `GameItems.PurchaseNotFound` | NotFound | Compra não existe |

### Ranking

| Código | Tipo | Quando acontece |
|---|---|---|
| `RankingGroups.NameIsNullOrWhiteSpace` | Validation | Nome do grupo vazio |
| `RankingGroups.NameTooLong` | Validation | Nome do grupo com mais de 40 caracteres |
| `RankingGroups.TooManyGroups` | Conflict | Criar ou entrar num 6º grupo |
| `RankingGroups.GroupFull` | Conflict | Entrar num grupo que já tem 20 membros |
| `RankingGroups.AlreadyMember` | Conflict | Entrar num grupo do qual já é membro |
| `RankingGroups.NotOwner` | Conflict | Renomear, gerar código novo ou remover membro sem ser o dono |
| `RankingGroups.CannotRemoveSelf` | Validation | O dono tentando se remover (ele sai do grupo) |
| `RankingGroups.NotFound` | NotFound | Grupo não existe, ou o usuário não é membro |
| `RankingGroups.InviteNotFound` | NotFound | Código de convite que não existe (ou foi trocado) |
| `RankingGroups.MemberNotFound` | NotFound | Remover alguém que não é membro do grupo |
| `Ranking.InvalidWeekStart` | Validation | Semana que não começa numa segunda (ou não informada, nas reações) |
| `Ranking.WeekOutOfRange` | Validation | Semana futura ou anterior à criação do grupo |
| `Ranking.MemberNotFound` | NotFound | Reagir a quem não é membro do grupo |
| `Ranking.CannotReactToSelf` | Validation | Reagir à própria semana |
| `Ranking.ReactionOutOfWindow` | Validation | Reagir a uma semana que não é a atual nem a anterior |
| `Ranking.UnknownReaction` | Validation | Reação que não é 👏 nem 🔥 |
