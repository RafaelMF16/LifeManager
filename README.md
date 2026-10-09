# LifeManager (API)

O LifeManager é uma ferramenta pessoal para organizar várias áreas da vida num só lugar. Este repositório é a **API** (.NET). O frontend é o [LifeManagerFront](https://github.com/RafaelMF16/LifeManagerFront), uma SPA em React.

## O que o projeto faz

- **Contas:** cadastro, login e sessão com renovação automática (access token + refresh token em cookie). Preferências de tema e idioma ficam salvas por usuário.
- **Finanças:** categorias, meses, lançamentos (receitas, gastos e investimentos), transações recorrentes lançadas automaticamente, metas mensais (limite de gastos e alvo de investimento) e um painel com a evolução do período.
- **Hábitos (gamificado):** hábitos para construir ou evitar, check-ins diários, ofensivas, um personagem com nível, XP, HP e moedas, e uma loja de recompensas que você mesmo define e paga com moedas.

As regras de cada área estão em [docs/regras-de-negocio.md](docs/regras-de-negocio.md).

## Stack

- .NET 10 / ASP.NET Core (Clean Architecture: `Domain`, `Application`, `Infrastructure`, `WebApi`)
- PostgreSQL 16 com EF Core + Npgsql (extensão `pg_trgm` para a busca)
- Autenticação JWT (access token de 15 min) + refresh token rotativo em cookie `HttpOnly`
- Testes com xUnit (`LifeManager.Domain.Test` e `LifeManager.Application.Test`)

## Como rodar localmente

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (para o PostgreSQL)
- Ferramenta do EF Core:

  ```bash
  dotnet tool install --global dotnet-ef
  ```

### 1. Subir o banco

Na raiz deste repositório:

```bash
docker compose up -d
```

Isso sobe um PostgreSQL 16 em `localhost:5432`, com banco, usuário e senha `lifemanager`. Os dados ficam no volume `lifemanager-postgres-data`.

### 2. Configurar os secrets

A API lê três chaves obrigatórias da configuração e não sobe se faltar alguma. Em desenvolvimento, use o user-secrets:

```bash
dotnet user-secrets set "lifeManagerConnectionString" "Host=localhost;Port=5432;Database=lifemanager;Username=lifemanager;Password=lifemanager" --project LifeManager.WebApi
dotnet user-secrets set "accessTokenSecretKey" "<chave aleatória com 32+ caracteres>" --project LifeManager.WebApi
dotnet user-secrets set "refreshTokenSecretKey" "<outra chave aleatória com 32+ caracteres>" --project LifeManager.WebApi
```

As chaves assinam os tokens com HMAC-SHA256, então precisam de pelo menos 32 caracteres. Use valores diferentes para cada uma. Uma forma de gerar:

```bash
openssl rand -base64 48
```

Opcional: `businessTimeZone` (id IANA, padrão `America/Sao_Paulo`) define o fuso do "hoje" usado pelas regras (check-ins, recorrências, resgates).

Fora do ambiente de desenvolvimento, as mesmas chaves podem vir de variáveis de ambiente.

### 3. Confiar no certificado HTTPS de desenvolvimento

O frontend chama a API em `https://localhost:7233`, e o cookie do refresh token é `Secure`:

```bash
dotnet dev-certs https --trust
```

### 4. Criar as tabelas

A API **não** aplica as migrations sozinha. Rode na raiz do repositório (e de novo sempre que puxar uma migration nova):

```bash
dotnet ef database update --project LifeManager.Infrastructure --startup-project LifeManager.WebApi
```

### 5. Rodar a API

```bash
dotnet run --project LifeManager.WebApi --launch-profile https
```

A API fica em `https://localhost:7233`. O CORS libera só o frontend em `https://localhost:5173`. Para subir o front, siga o README do [LifeManagerFront](https://github.com/RafaelMF16/LifeManagerFront).

Junto com a API sobem dois jobs em segundo plano, que rodam ao iniciar e depois a cada hora:

- **Recorrências:** lança as transações recorrentes cujo dia chegou.
- **Fechamento do dia dos hábitos:** avalia os dias que já fecharam (falhas, dias limpos, proteções).

### 6. Rodar os testes

```bash
dotnet test LifeManager.slnx
```

Os testes não usam banco: os repositórios são substituídos por mocks em memória.

## Documentação

- [docs/regras-de-negocio.md](docs/regras-de-negocio.md): as regras de negócio de cada área, em linguagem de produto.
- [CLAUDE.md](CLAUDE.md): referência técnica (arquitetura, convenções, endpoints e detalhes de implementação).
