# Spec: `habit-reminders` (back)

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (N3). Branch `feature/habit-reminders`, a partir da `dev`, nos dois repos, **depois** do `pwa` (o service worker vem de lá). Tela: [`LifeManagerFront/docs/specs/habit-reminders.md`](../../../LifeManagerFront/docs/specs/habit-reminders.md).

## Objetivo

Um hábito pode ter um horário de lembrete. Se, nesse horário, ele tem agenda para hoje e ainda não foi feito, o celular recebe uma notificação. Hoje o jogo só "cobra" no fechamento do dia, quando já é tarde.

## Decisões de desenho

- **Precisão de hora cheia.** O agendador de produção roda o `--run-jobs` uma vez por hora, no minuto 5 (`docs/deploy.md`). O lembrete é uma hora do dia (0–23, no fuso do negócio) e chega entre HH:05 e HH:10.
  - Precisão de minutos exigiria um agendador a cada 5 min (custo do Cloud Run Job) e fica fora.
- **Web Push padrão (VAPID)**, sem Firebase Cloud Messaging: o back assina e manda direto para o serviço de push do navegador.
  - Biblioteca: o pacote NuGet `WebPush` (dependência nova, **em discussão**).
- **Uma notificação por usuário por hora:** vários hábitos na mesma hora viram uma notificação só ("Faltam 2 hábitos: Ler, Academia"). Hábito medido mostra quanto falta ("Beber água: faltam 750 ml").
- Só hábitos **a construir**, ativos, com agenda hoje e não feitos. Hábitos a evitar não têm lembrete neste módulo.

## Contrato

| Endpoint | Body | Resposta |
|---|---|---|
| `GET /api/Push/PublicKey` | — | 200 `{ publicKey }` (a chave VAPID pública) |
| `PUT /api/Push/Subscriptions` | `{ endpoint, keys: { p256dh, auth }, label? }` | 204 (upsert pelo `endpoint`) |
| `DELETE /api/Push/Subscriptions` | `{ endpoint }` | 204 (idempotente) |
| `POST/PUT /api/Habits` | os campos atuais + `reminderHour?` (0–23 ou null) | como hoje |
| `GET /api/Habits/{id}` e a listagem | + `reminderHour` | como hoje |

`Habit.ReminderNotAllowed` (400) vale para hábito a evitar. `Habit.InvalidReminderHour` (400) vale para fora de 0–23.

## Modelo e migration (aditiva): `AddHabitReminders`

- **`Habits.ReminderHour`** (`smallint` nulo): parâmetro opcional no final de `Create`/`Update`/`FromPersistence`, como o `AutoTrigger`.
- **Tabela `PushSubscriptions`:** `Id`, `UserId` (FK `CASCADE`), `Endpoint` (único, ≤ 1000), `P256dh`, `Auth`, `Label` (≤ 60, ex.: "Chrome · Android"), `CreatedAt`, `LastSuccessAt`.
  - Um endpoint que aparece para outro usuário (outra conta no mesmo aparelho) **muda de dono** no upsert.
- **Tabela `HabitReminderLog`:** `(HabitId, Date)` único, FK `CASCADE`. É a trava contra lembrete duplicado: o job grava com `INSERT … ON CONFLICT DO NOTHING` **antes** de enviar, e só envia o que inseriu.
  - Duas execuções na mesma hora nunca mandam duas vezes.
  - O preço é que um envio que falha não é refeito na mesma hora. É aceito: o lembrete é "melhor esforço".

## Job: `HabitRemindersRunner`

- **Primeiro** no `--run-jobs`, para chegar o mais perto possível da hora.
- Um escopo por usuário. Busca os hábitos com `ReminderHour = hora atual do AppClock` e agenda hoje que ainda não foram feitos, com as regras de `IsDueOn` e do check-in de hoje.
- Monta a mensagem no idioma das preferências do usuário (pt-BR/en). Envia para cada assinatura do usuário.
- Se o serviço de push responde **404/410** (assinatura morta), apaga a assinatura. Outros erros são logados e o job segue.
- **Local:** um `HabitRemindersJob` hospedado, como os outros, a cada hora.
- **Payload** (JSON, ≤ 2 KB): `{ title, body, url: "/habits/today", tag: "habits-<date>-<hour>" }`. O service worker do front exibe a notificação.

## Configuração (obrigatória fora de Development)

- `vapidPublicKey`, `vapidPrivateKey` e `vapidSubject` (`mailto:` do remetente).
- Sem elas em Development, os lembretes ficam desligados com um log de aviso. Em outro ambiente, o startup falha (o mesmo padrão do `brevoApiKey`).
- `docs/deploy.md`: como gerar as chaves, guardá-las no Secret Manager e passá-las ao serviço e aos jobs no `deploy-api.ps1`.

## Testes

- **Domain:** `ReminderHour` (limites; recusado em hábito a evitar).
- **Application:**
  - o runner escolhe só os hábitos da hora, com agenda hoje e não feitos;
  - agrupa uma notificação por usuário;
  - texto de hábito medido;
  - idioma;
  - não reenvia na segunda execução (o log);
  - apaga a assinatura em 410;
  - o upsert troca o dono.
  - Um `PushSenderMock` com a lista do que foi enviado, como o `EmailSenderMock`.
- **Portão:** `dotnet build` com 0 warnings e `dotnet test` verde.

## Docs

`CLAUDE.md` (Habits, jobs, configuração), `docs/regras-de-negocio.md` (quando o lembrete chega e quando não chega, os códigos novos) e `docs/deploy.md` (chaves VAPID, ordem dos jobs).

## Arquivos

`Domain/Habits/` (VO + regra), `Domain/Push/` (entidade, interface `IPushSender`), `Application/Push/`, `Application/Habits/` (DTOs), `Infrastructure/Push/` (repositório, `WebPushSender`), `Infrastructure/Migrations/`, `WebApi/Push/Controllers/`, `WebApi/Habits/Jobs/HabitRemindersRunner.cs`, `RunJobsMode.cs`, os testes e as docs.

## Perguntas em aberto

1. **Dependência `WebPush` e chaves VAPID no deploy:** em discussão. O módulo só começa depois dessa conversa.

Decidido (09/11/2026): lembrete em **hora cheia**, com o agendador atual.
