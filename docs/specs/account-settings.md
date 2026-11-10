# Spec: `account-settings` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (N1). Branch `feature/account-settings`, a partir da `dev`, nos dois repos. Tela: [`LifeManagerFront/docs/specs/account-settings.md`](../../../LifeManagerFront/docs/specs/account-settings.md).

## Objetivo

Dar ao usuário controle sobre a própria conta: mudar o nome, trocar a senha estando logado, sair de todos os aparelhos, baixar os próprios dados e excluir a conta com prazo de arrependimento. Exportar e excluir são direitos previstos na LGPD. Hoje o back só tem `GET /api/Users/Me`.

## Contrato

| Endpoint | Body | Resposta | Erros |
|---|---|---|---|
| `PUT /api/Users/Me` | `{ name }` | 200 `{ name }` | as regras de `UserName` (`User.Name*`) |
| `POST /api/Auth/ChangePassword` `[Authorize]` | `{ currentPassword, newPassword }` | 204 | `User.WrongPassword` (400), as regras de `PlainPassword` |
| `POST /api/Auth/LogoutAll` `[Authorize]` | — | 204 e apaga o cookie | — |
| `GET /api/Users/Me/Export` | — | 200 `application/json`, `Content-Disposition: attachment; filename="lifemanager-<data>.json"` | 429 (rate limit) |
| `POST /api/Users/Me/Deletion` | `{ password }` | 202 `{ scheduledFor }` e apaga o cookie | `User.WrongPassword` (400) |

- **Por que trocar a senha e sair de todos os aparelhos ficam em `/api/Auth`:** o cookie `__session` tem `Path=/api/Auth`. Só ali o back sabe qual é a sessão atual e consegue mantê-la.
- **Senha errada é 400, não 401.** O front trata qualquer 401 fora de `/api/Auth` como token expirado e tenta renovar.

## Regras

### Nome

`User.Rename(UserName)` e `ExecuteUpdateAsync` só do `Name`. O detector de transferências próprias da importação já lê o `User.Name` atual, então nada mais muda.

### Senha

- Confere a senha atual com BCrypt e grava o novo hash.
- Revoga **todas as outras** sessões, mantendo a do cookie atual (`IRefreshTokenRepository.RevokeAllActiveByUserIdExceptAsync(userId, currentHash)`).
- Sem cookie, revoga todas.

### Sair de todos os aparelhos

`RevokeAllActiveByUserIdAsync` (já existe), apaga o cookie e devolve 204. O access token atual continua válido até expirar (≤ 15 min), como no logout normal.

### Rate limit

Política nova `AccountSensitive`: 10 por 10 min **por usuário**, nas rotas de senha, exclusão e exportação. A exportação tem um limite próprio de 3 por hora.

### Exportar

- Um JSON escrito em streaming com `Utf8JsonWriter` direto na resposta. O serviço recebe um `Stream`, e as leituras são `AsNoTracking` com `AsAsyncEnumerable`, para não carregar tudo na memória.
- **Seções:**
  - `user` (nome, e-mail, data de criação), `preferences`;
  - `categories`, `months`, `transactions`, `recurringTransactions`, `budgetPlans`, `accountHolders`;
  - `statementImports`: só o histórico, sem linhas de rascunho;
  - `habits`, `checkIns`, `progressEntries`, `player`, `ledger`, `rewards`, `redemptions`, `gameItemPurchases`, `achievements`, `challenges`;
  - `rankingGroups`: só os nomes dos grupos e a data de entrada. **Nenhum dado de outros membros.**
- **Nunca sai:** hash de senha, tokens, tentativas de login, chaves de conta (`accountKey`, `ExternalId`).
- `version: 1` no topo.

### Excluir, com prazo

- **Pedido:**
  - Confere a senha;
  - grava `DeletionRequestedAt = now` e `DeletionScheduledFor = now + AccountDeletion.GraceDays` (**7 dias**);
  - revoga todas as sessões e apaga o cookie;
  - manda um e-mail no idioma da conta: "Sua conta será excluída em dd/mm. Entre de novo até lá para cancelar." O envio que falha é só logado, como nos outros e-mails.
  - Pedir de novo durante o prazo não muda a data (é idempotente).
- **Durante o prazo:** a conta funciona normalmente se a pessoa entrar. **Um login bem-sucedido cancela a exclusão** (zera os dois campos), e o `LoginResponseDto` traz `deletionCancelled: true` para o front avisar.
  - "Esqueci a senha" continua funcionando: é um caminho para voltar.
  - Os grupos de ranking continuam com a pessoa até a exclusão.
- **Exclusão definitiva:** um job novo, `AccountDeletionRunner` (de hora em hora; último no `--run-jobs`), processa um usuário por escopo. Numa transação só:
  1. `SELECT … FROM "Users" WHERE "Id" = @id AND "DeletionScheduledFor" <= now FOR UPDATE`. Nenhuma linha quer dizer que a pessoa entrou e cancelou, e o job segue para o próximo.
  2. **Sai de cada grupo de ranking** com a regra de saída que já existe (`RankingGroupMembership.OwnerAfterLeaving`: o membro mais antigo vira dono; o último a sair apaga o grupo). **Isso é obrigatório:** a FK `RankingGroups.OwnerUserId` é `CASCADE`, e apagar o dono direto apagaria o grupo de todos os membros.
  3. `DELETE FROM "Users"`. Todas as tabelas do usuário caem em cascata. Isso já foi provado num banco real pelo `UserDeletionTests` do `integration-tests` (PR #43), que corrigiu um bug no caminho. As quatro FKs `NO ACTION` (`HabitCheckIns.UserId`, `HabitProgressEntries.UserId`, `Transactions.CategoryId` e `RecurringTransactions.CategoryId`) travavam o `DELETE` com 23503. A causa é que o Postgres checa o `NO ACTION` no fim de cada cascata, e não no fim do comando. A migration `MakeUserDeletionForeignKeysDeferrable` as deixou `DEFERRABLE INITIALLY DEFERRED`, checadas no commit.
- Os resultados de semanas já fechadas (`RankingWeekResults`) perdem a linha da pessoa, por causa da FK `CASCADE`. É aceito e fica registrado em `docs/regras-de-negocio.md`.

## Migration (aditiva)

`AddUserDeletionSchedule`: em `Users`, `DeletionRequestedAt` e `DeletionScheduledFor` (`timestamptz`, nulos) e um índice parcial em `DeletionScheduledFor WHERE NOT NULL`, para a busca do job.

## Testes

- **Domain:** `User.Rename`; `User.ScheduleDeletion(now)` (idempotente) e `CancelDeletion()`.
- **Application:**
  - nome válido e inválido;
  - troca de senha: senha errada, senha nova inválida, e outra sessão revogada enquanto a atual fica;
  - `LogoutAll`;
  - pedido de exclusão: senha errada, e-mail enviado, sessões revogadas, idempotência;
  - login dentro do prazo cancela (`deletionCancelled`);
  - `AccountDeletionService`: não apaga antes do prazo; passa a posse do grupo para o membro mais antigo; apaga o grupo em que a pessoa estava sozinha; não apaga quem cancelou entre a busca e o lock (um hook no mock).
- **Exportação:** um teste de serviço que lê o JSON gerado e confere as seções e a ausência de `passwordHash`/tokens.
- **Integração** (o `integration-tests` já está na `dev`): o `UserDeletionTests` já confere o `DELETE` do usuário, comparando a contagem de cada tabela. Este módulo acrescenta o `AccountDeletionRunner` de ponta a ponta, com a saída dos grupos de ranking (o teste atual não semeia grupo) e a transação com o lock.

## Docs

- `CLAUDE.md`: Users e Auth, o job novo e os rate limits.
- `docs/regras-de-negocio.md`: prazo, cancelamento por login, ranking, o que a exportação contém e o código `User.WrongPassword`.
- `docs/deploy.md`: o job novo entra no `--run-jobs`, sem configuração nova.

## Arquivos

`Domain/Users/` (métodos, erros), `Domain/Auth/Interfaces/IRefreshTokenRepository.cs`, `Application/Users/` (`UserAccountService`, `AccountExportService`, `AccountDeletionService`), `Application/Auth/Emails/AuthEmailTemplates.cs`, `Infrastructure/Users/` (repositório, exportação), `Infrastructure/Migrations/`, `WebApi/Users/Controllers/`, `WebApi/Auth/Controllers/`, `WebApi/Users/Jobs/AccountDeletionRunner.cs`, `RunJobsMode.cs`, os testes e as docs.

## Perguntas em aberto

Nenhuma. **Prazo decidido: 7 dias** (09/11/2026), na constante `AccountDeletion.GraceDays`.
