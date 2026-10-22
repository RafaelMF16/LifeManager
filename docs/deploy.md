# Deploy no Google Cloud

Como o LifeManager roda em produção e como publicar. O passo a passo do front (Firebase Hosting) está no README do [LifeManagerFront](https://github.com/RafaelMF16/LifeManagerFront).

## Arquitetura

```
Navegador ──► Firebase Hosting (https://<projeto>.web.app)
               ├── /assets, /index.html ......... arquivos estáticos do front
               └── /api/** ── rewrite ──► Cloud Run "lifemanager-api" ──► Cloud SQL "lifemanager-db" (PostgreSQL 16)
Cloud Scheduler (a cada hora, :05) ──► Cloud Run Job "lifemanager-jobs" (--run-jobs) ──┤
deploy-api.ps1 (a cada deploy) ──────► Cloud Run Job "lifemanager-migrate" (--migrate) ┘
```

- **Uma origem só.** O Firebase Hosting repassa `/api/**` para o Cloud Run, então o navegador fala só com `web.app`. Não há CORS em produção, e o cookie do refresh token é de primeira parte. O Hosting só repassa o cookie chamado `__session`, por isso esse é o nome do cookie (`RefreshTokenCookie`).
- **A API escala a zero.** Sem tráfego, nenhuma instância fica ligada. Por isso os jobs de hora em hora não rodam dentro da API (`backgroundJobs__enabled=false`): o Cloud Scheduler dispara um Cloud Run Job com a mesma imagem e o argumento `--run-jobs`, que roda recorrências, fechamento do dia e a limpeza de importações de extrato abandonadas (rascunhos com mais de 30 dias) uma vez e sai.
- **As migrations rodam no deploy.** Antes de atualizar o serviço, o script de deploy executa o Cloud Run Job `lifemanager-migrate`, que roda a mesma imagem com `--migrate`. Se ele falhar, nada mais muda (veja [Migrations](#migrations)).
- **Segredos** ficam no Secret Manager e chegam como variáveis de ambiente com os mesmos nomes que a API já lê: `lifeManagerConnectionString`, `accessTokenSecretKey` e `refreshTokenSecretKey`.
- **Região:** `southamerica-east1` (São Paulo) para tudo.

## Custos

Referência para ~100 usuários:

| Item | US$/mês |
|---|---|
| Cloud SQL `db-f1-micro` + SSD 10 GB + backups de 7 dias | ~15 |
| Cloud Run (serviço e jobs), Scheduler, Secret Manager, Artifact Registry, Firebase Hosting | ~0–2 (níveis gratuitos) |

O Cloud SQL cobra desde a criação, mesmo sem uso. Para pausar a cobrança de CPU e memória, use `gcloud sql instances patch lifemanager-db --activation-policy=NEVER`; o disco continua sendo cobrado.

## Pré-requisitos (uma vez por máquina)

```powershell
winget install Google.CloudSDK
gcloud init                                   # login + projeto padrão
gcloud auth application-default login         # usado pelo cloud-sql-proxy
gcloud components install cloud-sql-proxy
```

Você precisa ser **Proprietário (Owner)** do projeto. Se a conta de faturamento for de outra pessoa, ela te adiciona em **IAM e administrador → Conceder acesso**.

## Setup único do projeto

Rode no PowerShell, na raiz deste repositório. Defina as variáveis primeiro:

```powershell
$P = '<PROJECT_ID>'
$R = 'southamerica-east1'
gcloud config set project $P
```

### 1. APIs e repositório de imagens

```powershell
gcloud services enable run.googleapis.com sqladmin.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com secretmanager.googleapis.com cloudscheduler.googleapis.com iam.googleapis.com
gcloud artifacts repositories create lifemanager --repository-format=docker --location=$R
```

O Cloud Build usa a service account padrão do Compute para buildar. Garanta que ela pode publicar a imagem e escrever logs:

```powershell
$N = gcloud projects describe $P --format='value(projectNumber)'
foreach ($role in 'roles/artifactregistry.writer', 'roles/logging.logWriter', 'roles/storage.objectViewer') {
    gcloud projects add-iam-policy-binding $P --member="serviceAccount:$N-compute@developer.gserviceaccount.com" --role=$role --condition=None
}
```

### 2. Banco (Cloud SQL), que começa a cobrar aqui

A criação leva cerca de 10 minutos.

```powershell
gcloud sql instances create lifemanager-db --database-version=POSTGRES_16 --edition=ENTERPRISE --tier=db-f1-micro --region=$R --storage-type=SSD --storage-size=10 --backup-start-time=06:00 --retained-backups-count=7
gcloud sql databases create lifemanager --instance=lifemanager-db
```

Crie o usuário da aplicação com uma senha aleatória. Guarde a senha: ela vai para o secret da string de conexão e é usada nas migrations.

```powershell
function New-RandomSecret { $bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes) }
$DbPassword = (New-RandomSecret) -replace '[+/=]', ''
gcloud sql users create lifemanager --instance=lifemanager-db --password=$DbPassword
```

### 3. Secrets

O helper abaixo grava o valor num arquivo temporário **sem quebra de linha no fim** (um `|` do PowerShell adicionaria uma) e apaga o arquivo depois.

```powershell
function Set-Secret($name, $value) {
    $file = New-TemporaryFile
    [IO.File]::WriteAllText($file, $value)
    gcloud secrets create $name --data-file=$file --replication-policy=automatic
    Remove-Item $file
}

Set-Secret 'lifeManagerConnectionString' "Host=/cloudsql/${P}:${R}:lifemanager-db;Database=lifemanager;Username=lifemanager;Password=$DbPassword;Maximum Pool Size=5"
Set-Secret 'accessTokenSecretKey' (New-RandomSecret)
Set-Secret 'refreshTokenSecretKey' (New-RandomSecret)
```

- Nunca reaproveite as chaves de desenvolvimento.
- O `Maximum Pool Size=5` mantém as conexões abaixo do limite do `db-f1-micro` (~25), mesmo com 3 instâncias mais o job.
- Para trocar um valor depois: `gcloud secrets versions add <nome> --data-file=<arquivo>`, e depois faça um novo deploy.

### 4. Service accounts

```powershell
gcloud iam service-accounts create lifemanager-run --display-name='LifeManager API e jobs'
gcloud iam service-accounts create lifemanager-scheduler --display-name='LifeManager Cloud Scheduler'

foreach ($role in 'roles/cloudsql.client', 'roles/secretmanager.secretAccessor') {
    gcloud projects add-iam-policy-binding $P --member="serviceAccount:lifemanager-run@$P.iam.gserviceaccount.com" --role=$role --condition=None
}
```

### 5. Primeiro deploy

```powershell
./deploy/deploy-api.ps1 -ProjectId $P
```

O script cria o job `lifemanager-migrate` e aplica todas as migrations no banco vazio. Depois cria o serviço `lifemanager-api` e o job `lifemanager-jobs`.

### 6. Agendamento dos jobs

O job precisa existir, então este passo vem depois do primeiro deploy.

```powershell
gcloud run jobs add-iam-policy-binding lifemanager-jobs --region=$R --member="serviceAccount:lifemanager-scheduler@$P.iam.gserviceaccount.com" --role=roles/run.invoker
gcloud scheduler jobs create http lifemanager-hourly --location=$R --schedule='5 * * * *' --time-zone='America/Sao_Paulo' --uri="https://run.googleapis.com/v2/projects/$P/locations/$R/jobs/lifemanager-jobs:run" --http-method=POST --oauth-service-account-email="lifemanager-scheduler@$P.iam.gserviceaccount.com"
gcloud run jobs execute lifemanager-jobs --region=$R --wait      # teste imediato
```

### 7. Orçamento

No console, em **Faturamento → Orçamentos e alertas**, crie um orçamento (ex.: US$ 100) com alertas em 25%, 50% e 100%.

## Migrations

As migrations rodam sozinhas a cada deploy. Depois do build, o script atualiza o job `lifemanager-migrate` com a imagem nova e o executa, esperando o fim (`--wait`). O job chama o `MigrateAsync` do EF Core: aplica as pendentes, ou só registra "No pending migrations".

- **Se falhar**, o script para. O serviço e o `lifemanager-jobs` continuam com a imagem anterior. O erro aparece no log da execução: `gcloud run jobs executions list --job=lifemanager-migrate --region=$R` e depois Console → Cloud Run → Jobs → `lifemanager-migrate` → Logs.
- **Também falha** quando o modelo mudou e ninguém gerou a migration. Nesse caso, gere a migration e publique de novo.
- **Toda migration precisa ser aditiva.** Durante alguns segundos, a revisão anterior da API continua atendendo com o schema novo. Então crie colunas e tabelas primeiro, e remova ou renomeie só num deploy posterior, quando nenhum código publicado usar mais o formato antigo.

### Rodar à mão (inspecionar o banco, reverter uma migration)

Use o túnel do Cloud SQL Auth Proxy:

```powershell
# Terminal 1: túnel até o Cloud SQL (fica rodando)
cloud-sql-proxy "${P}:${R}:lifemanager-db" --port 5433

# Terminal 2: na raiz deste repositório
dotnet ef database update --project LifeManager.Infrastructure --startup-project LifeManager.WebApi --connection "Host=localhost;Port=5433;Database=lifemanager;Username=lifemanager;Password=<senha do banco>"
```

Se não tiver a senha em mãos: `gcloud secrets versions access latest --secret=lifeManagerConnectionString`.

## Deploys seguintes

```powershell
./deploy/deploy-api.ps1 -ProjectId <PROJECT_ID>
```

O script:
1. builda a imagem no Cloud Build (`cloudbuild.yaml`), com a tag `<commit>-<data>`;
2. aplica as migrations pelo job `lifemanager-migrate` e para se ele falhar;
3. atualiza o serviço;
4. aponta o job `lifemanager-jobs` para a mesma imagem.

## Operação

- **Logs:** console → Cloud Run → `lifemanager-api` ou `lifemanager-jobs` → Logs. Ou `gcloud run services logs read lifemanager-api --region=$R`.
- **Execuções dos jobs:** `gcloud run jobs executions list --job=lifemanager-jobs --region=$R` (ou `--job=lifemanager-migrate` para as migrations de cada deploy).
- **Imagens antigas:** apague no Artifact Registry. O nível gratuito é de 0,5 GB, e cada imagem tem ~100–200 MB.
- **Backup manual:** `gcloud sql backups create --instance=lifemanager-db`.
- **Fim do Free Trial (90 dias):** faça o upgrade para conta paga (o custo segue em ~US$ 15/mês) ou exporte o banco antes de desligar:
  ```powershell
  gcloud sql export sql lifemanager-db gs://<bucket>/lifemanager.sql --database=lifemanager
  ```
  Sem o upgrade, os recursos param no fim do trial e os dados podem ser apagados 30 dias depois.
