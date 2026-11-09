# Spec: `clock-fixes`

Módulo do mapa [`LifeManagerFront/docs/specs/README.md`](../../../LifeManagerFront/docs/specs/README.md) (P1 + P2). Branch `fix/clock-fixes`, a partir da `dev`.

## Objetivo

Todo "agora" e "hoje" do back passa pelo `TimeProvider`/`AppClock`, para as regras seguirem o fuso do negócio e serem testáveis.

**O bug (P1):** o ano de um mês novo vem de `DateTimeOffset.UtcNow.Year`, em dois lugares:
- `MonthlySummaryService.CreateAsync` passa o ano;
- `MonthlySummaryYear.Create` valida contra ele.

Em 31/12, das 21h às 24h (Brasília), criar "Dezembro" cria **dezembro do ano seguinte**, enquanto o front mostra o ano corrente desabilitado.

## Escopo

1. `MonthlySummaryYear.Create(int value, int currentYear)` compara com o parâmetro e não lê relógio. `MonthlySummary.Create(idUser, month, year, currentYear)` repassa.
2. `MonthlySummaryService` recebe `AppClock` e usa `_appClock.Today().Year` como ano do mês **e** como ano corrente.
3. `TokenService` recebe `TimeProvider`. As linhas 72, 122 e 155 usam `_timeProvider.GetUtcNow()`, e o `Expires` do JWT sai do mesmo `now`.
4. `RefreshToken.CreatedAt` e `User.CreationDate` deixam de ter o inicializador `= DateTimeOffset.UtcNow`: o valor vem do factory, recebido como parâmetro.
   - **Antes de mexer:** conferir no mapeamento EF se essas propriedades são persistidas. Se forem só leitura em memória, basta remover o inicializador onde não é usado.
   - **Sem migration:** nenhuma coluna muda.

Fora do escopo: liberar o ano anterior (fica no `quick-transaction`, que depende deste módulo) e o relógio do navegador no front (P9/N6).

## Critérios de aceite

- Com o `FakeTimeProvider` em **31/12/2026 às 22h de Brasília** (01/01/2027 01:00 UTC), `POST MonthlySummaries { month: 12 }` cria **dezembro de 2026**.
- Com o relógio em 01/01/2027 às 10h de Brasília, criar dezembro retorna `MonthlySummary.YearNotCurrent` (a regra atual continua igual).
- `grep -rn "UtcNow\|DateTime.Now" LifeManager.Application LifeManager.Domain` só encontra `_timeProvider.GetUtcNow()` (e o `AppClock`).
- O código de erro e as respostas da API não mudam.

## Testes

- **Domain:** `MonthlySummaryYearTests` passa a usar `currentYear` explícito (sem `DateTimeOffset.UtcNow` no teste) e ganha o caso ano ≠ corrente.
- **Application:** `MonthlySummaryServiceTest` ganha os dois casos de virada de ano acima, com `SetToday` ou um `SetUtcNow` novo no `FakeTimeProvider` (hoje ele só tem `SetToday`, ao meio-dia).
- **Application:** um teste de `TokenService` em que avançar o `FakeTimeProvider` além de 7 dias torna o refresh inválido, se o mock do repositório permitir. Se não permitir, fica registrado como limitação.
- **Portão:** `dotnet build LifeManager.slnx` sem warnings novos e `dotnet test LifeManager.slnx` verde.

## Arquivos

`LifeManager.Domain/MonthlySummaries/ValueObjects/MonthlySummaryYear.cs`, `.../MonthlySummary.cs`, `LifeManager.Application/MonthlySummaries/Services/MonthlySummaryService.cs`, `LifeManager.Application/Auth/Services/TokenService.cs`, `LifeManager.Domain/Auth/RefreshToken.cs`, `LifeManager.Domain/Users/User.cs`, os testes correspondentes e `Configurations/FakeTimeProvider.cs`.

## Docs

- `CLAUDE.md`, seção MonthlySummaries: "current UTC year" → "current year in the business time zone (`AppClock`)".
- `docs/regras-de-negocio.md`: a regra já diz "ano corrente no fuso do negócio", então nada muda.
