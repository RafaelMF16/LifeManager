# Deploy no Google Cloud

Como o LifeManager roda em produção e como publicar. O passo a passo do front (Firebase Hosting) está no README do [LifeManagerFront](https://github.com/RafaelMF16/LifeManagerFront).

## Arquitetura

```
Navegador ──► Firebase Hosting (https://<projeto>.web.app)
               ├── /assets, /index.html ......... arquivos estáticos do front
               └── /api/** ── rewrite ──► Cloud Run "lifemanager-api" ──► Cloud SQL "lifemanager-db" (PostgreSQL 16)
Cloud Scheduler (a cada hora, :05) ──► Cloud Run Job "lifemanager-jobs" (--run-jobs) ──┤
deploy-api.ps1 (a cada deploy) ──────► Cloud Run Job "lifemanager-migrate" (--migrate) ┘
GitHub Actions (merge na main) ──► testes ──► deploy-api.ps1
```

- **Uma origem só.** O Firebase Hosting repassa `/api/**` para o Cloud Run, então o navegador fala só com `web.app`. Não há CORS em produção, e o cookie do refresh token é de primeira parte. O Hosting só repassa o cookie chamado `__session`, por isso esse é o nome do cookie (`RefreshTokenCookie`).
- **A API escala a zero.** Sem tráfego, nenhuma instância fica ligada. Por isso os jobs de hora em hora não rodam dentro da API (`backgroundJobs__enabled=false`): o Cloud Scheduler dispara um Cloud Run Job com a mesma imagem e o argumento `--run-jobs`, que roda recorrências, fechamento do dia, a limpeza de importações de extrato abandonadas (rascunhos com mais de 30 dias) e a limpeza da autenticação por e-mail (cadastros pendentes e links de recuperação vencidos há mais de 7 dias, controles de reenvio com mais de 1 dia) uma vez e sai.
- **As migrations rodam no deploy.** Antes de atualizar o serviço, o script de deploy executa o Cloud Run Job `lifemanager-migrate`, que roda a mesma imagem com `--migrate`. Se ele falhar, nada mais muda (veja [Migrations](#migrations)).
- **Segredos** ficam no Secret Manager e chegam como variáveis de ambiente com os mesmos nomes que a API já lê: `lifeManagerConnectionString`, `accessTokenSecretKey`, `refreshTokenSecretKey`, `brevoApiKey` e `emailSenderAddress`.
- **E-mails** (confirmação de cadastro e recuperação de senha) saem pelo [Brevo](https://www.brevo.com), no plano gratuito (300 e-mails por dia). Os links apontam para `frontendBaseUrl`, que o script de deploy define como `https://<projeto>.web.app` (parâmetro `-FrontendBaseUrl`). O serviço **e os dois jobs** recebem essas chaves, porque todos montam os mesmos serviços na inicialização, e a API não sobe fora de Development sem elas.
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
Set-Secret 'brevoApiKey' '<chave da API do Brevo>'          # veja "E-mail (Brevo)" abaixo
Set-Secret 'emailSenderAddress' '<remetente verificado no Brevo>'
```

- Nunca reaproveite as chaves de desenvolvimento.
- O `Maximum Pool Size=5` mantém as conexões abaixo do limite do `db-f1-micro` (~25), mesmo com 3 instâncias mais o job.
- Para trocar um valor depois: `gcloud secrets versions add <nome> --data-file=<arquivo>`, e depois faça um novo deploy.

#### E-mail (Brevo)

Sem domínio próprio, o remetente é um endereço comum (ex.: um Gmail) verificado no Brevo:

1. Crie uma conta gratuita em [brevo.com](https://www.brevo.com).
2. Em **Senders, Domains & Dedicated IPs → Senders → Add a sender**, cadastre o endereço remetente e confirme pelo código que o Brevo envia para ele. Esse endereço vai no secret `emailSenderAddress`.
3. Em **SMTP & API → API Keys → Generate a new API key**, gere a chave. Ela vai no secret `brevoApiKey` e não aparece de novo no Brevo.
4. Opcional: o nome que aparece como remetente é `LifeManager`. Para trocar, defina a variável `emailSenderName` (acrescente-a ao `$envVars` do `deploy/deploy-api.ps1`).

- Sem domínio próprio (sem SPF/DKIM do remetente), parte dos e-mails pode cair no spam. As telas pedem para o usuário conferir a caixa de spam.
- Os secrets precisam existir **antes** do deploy que passa a usá-los. Se faltarem, o `gcloud run jobs deploy lifemanager-migrate` falha e nada muda em produção.
- Uma falha de envio (chave inválida, limite diário) não aparece para o usuário: a API registra o erro no log (`Brevo rejected the e-mail` / `Could not reach Brevo`) e responde normalmente.
- O limite por IP dos endpoints de e-mail usa o IP do cliente que chega pelos cabeçalhos repassados. No primeiro deploy, confira nos logs (`Rate limit hit on ... for <IP>`, só aparece quando o limite é atingido) se o IP é do usuário e não do Firebase Hosting; se for do Hosting, todos os usuários dividem o mesmo limite.

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

Depois do setup de [Deploy automático](#deploy-automático-github-actions), cada merge na `main` publica sozinho. À mão, a partir de qualquer branch, o deploy é o mesmo script que o workflow chama:

```powershell
./deploy/deploy-api.ps1 -ProjectId <PROJECT_ID>
```

O script, nos dois caminhos:
1. builda a imagem no Cloud Build (`cloudbuild.yaml`), com a tag `<commit>-<data>`;
2. aplica as migrations pelo job `lifemanager-migrate` e para se ele falhar;
3. atualiza o serviço;
4. aponta o job `lifemanager-jobs` para a mesma imagem.

## Deploy automático (GitHub Actions)

Cada merge na `main` publica o repositório em produção sozinho: a API pelo `.github/workflows/deploy.yml` deste repositório, e o front pelo do [LifeManagerFront](https://github.com/RafaelMF16/LifeManagerFront). O pipeline não adiciona custo: o Actions é ilimitado em repositório público, e o resto entra nos níveis gratuitos do GCP.

- **API:** o job `test` roda `dotnet test LifeManager.slnx`. Só se ele passar, o job `deploy` autentica no GCP e roda o mesmo `./deploy/deploy-api.ps1` do deploy manual (build → migrations → serviço → job).
- **Front:** lint, testes e build. Só se passarem, o `firebase deploy --only hosting`.
- **Fila:** dois merges seguidos publicam um depois do outro, nunca em paralelo (`concurrency` sem cancelamento, no run inteiro, para que um merge mais novo nunca publique antes de um mais antigo). O GitHub guarda só um run na fila: com três merges seguidos, o do meio é cancelado, e o último publica a `main` com tudo.
- **Logs públicos:** o repositório é público, então os logs do Actions também são. Nenhum segredo passa pelo GitHub (os do banco e do JWT ficam no Secret Manager), e o script só imprime o nome da imagem. O arquivo de credenciais que o `google-github-actions/auth` grava no workspace (`gha-creds-*.json`) está no `.gitignore`, no `.gcloudignore` e no `.dockerignore`, então não vai para o Cloud Build nem para a imagem.
- **Sem chave guardada no GitHub.** O Actions troca o token OIDC do próprio run por uma credencial de curta duração da service account `github-deployer` (Workload Identity Federation). No GitHub ficam só três variáveis, que são identificadores e não credenciais.
- **Só a `main` dos dois repositórios autentica.** O provider rejeita qualquer token de outro dono ou de outra branch, e cada repositório só pode usar a service account pelo seu próprio nome. Um fork ou um PR de terceiros nunca consegue credencial. Os workflows disparam só em `push` na `main` e em `workflow_dispatch`.

### Setup único

Rode no PowerShell, depois do primeiro deploy manual (o bucket do Cloud Build e os serviços já precisam existir), tudo **na mesma janela**: os passos seguintes usam as variáveis criadas nos anteriores (`$D`, `$N`). Defina `$P` e `$R` antes, mesmo que o `gcloud config` já aponte para o projeto. Com o `$P` vazio, o `$D` sai inválido e o `gcloud` só responde `INVALID_ARGUMENT: Unknown error`.

```powershell
$P = '<PROJECT_ID>'
$R = 'southamerica-east1'
gcloud config set project $P
```

#### 1. APIs da federação

```powershell
gcloud services enable iamcredentials.googleapis.com sts.googleapis.com
```

#### 2. Pool e provider do GitHub

O `--attribute-condition` é o que barra forks e outras branches: só tokens do dono `RafaelMF16` e da `main` passam.

```powershell
gcloud iam workload-identity-pools create github --location=global --display-name='GitHub Actions'
gcloud iam workload-identity-pools providers create-oidc github-oidc --location=global --workload-identity-pool=github --display-name='GitHub OIDC' --issuer-uri='https://token.actions.githubusercontent.com' --attribute-mapping='google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.repository_owner=assertion.repository_owner,attribute.ref=assertion.ref' --attribute-condition="assertion.repository_owner == 'RafaelMF16' && assertion.ref == 'refs/heads/main'"
```

#### 3. Service account do deploy

Só os dois repositórios podem usar a `github-deployer`:

```powershell
gcloud iam service-accounts create github-deployer --display-name='GitHub Actions deploy'
$D = "github-deployer@$P.iam.gserviceaccount.com"
$Pool = gcloud iam workload-identity-pools describe github --location=global --format='value(name)'
foreach ($repo in 'RafaelMF16/LifeManager', 'RafaelMF16/LifeManagerFront') {
    gcloud iam service-accounts add-iam-policy-binding $D --role=roles/iam.workloadIdentityUser --member="principalSet://iam.googleapis.com/$Pool/attribute.repository/$repo"
}
```

Os papéis, sem `owner` nem `editor`:

```powershell
$N = gcloud projects describe $P --format='value(projectNumber)'
foreach ($role in 'roles/cloudbuild.builds.editor', 'roles/serviceusage.serviceUsageConsumer', 'roles/logging.viewer', 'roles/storage.bucketViewer', 'roles/run.developer', 'roles/artifactregistry.reader', 'roles/firebasehosting.admin') {
    gcloud projects add-iam-policy-binding $P --member="serviceAccount:$D" --role=$role --condition=None
}
gcloud storage buckets add-iam-policy-binding "gs://${P}_cloudbuild" --member="serviceAccount:$D" --role=roles/storage.admin
foreach ($sa in "lifemanager-run@$P.iam.gserviceaccount.com", "$N-compute@developer.gserviceaccount.com") {
    gcloud iam service-accounts add-iam-policy-binding $sa --member="serviceAccount:$D" --role=roles/iam.serviceAccountUser
}
```

| Papel | Para quê |
|---|---|
| `cloudbuild.builds.editor`, `serviceusage.serviceUsageConsumer` | `gcloud builds submit` |
| `storage.admin` **só no bucket** `<projeto>_cloudbuild` | enviar o código-fonte para o build |
| `storage.bucketViewer` no projeto | só nomes e metadados de bucket, sem acesso a arquivos. O `gcloud builds submit` lista os buckets do projeto para confirmar que o bucket padrão pertence a ele (proteção contra outro projeto registrar o mesmo nome). Sem isso, o build falha com `The user is forbidden from accessing the bucket`, uma mensagem que fala de `serviceusage.services.use` mas não é sobre ele |
| `logging.viewer` | acompanhar o log do build (`CLOUD_LOGGING_ONLY` no `cloudbuild.yaml`) |
| `iam.serviceAccountUser` **só** na SA do Compute | o build roda como ela |
| `run.developer` | publicar o serviço e os jobs e executar o `lifemanager-migrate`. Também cobre o `run.services.get` que o rewrite `/api/**` do Hosting exige |
| `iam.serviceAccountUser` **só** na `lifemanager-run` | o serviço e os jobs rodam como ela |
| `artifactregistry.reader` | ler a imagem publicada |
| `firebasehosting.admin` | publicar o front |

Se o primeiro run falhar por falta de permissão, a mensagem do `gcloud` (ou do `firebase`) diz qual falta. Acrescente o papel aqui e reexecute (veja [Reexecutar](#reexecutar)). Nada muda em produção num run que falha.

#### 4. Limpeza das imagens

O nível gratuito do Artifact Registry é de 0,5 GB, e cada imagem tem ~100–200 MB. A política mantém as 3 imagens mais recentes e apaga o resto. Ela roda uma vez por dia, então o repositório pode passar de 3 por algumas horas.

```powershell
$Policy = New-TemporaryFile
[IO.File]::WriteAllText($Policy, '[{"name":"keep-3-most-recent","action":{"type":"Keep"},"mostRecentVersions":{"keepCount":3}},{"name":"delete-the-rest","action":{"type":"Delete"},"condition":{"tagState":"any"}}]')
gcloud artifacts repositories set-cleanup-policies lifemanager --location=$R --policy=$Policy --no-dry-run
Remove-Item $Policy
```

Isso também limita o rollback da API às 3 últimas imagens.

#### 5. Retenção do Hosting

Cada deploy do front guarda uma versão no Firebase Hosting (10 GB gratuitos). No Console do Firebase → **Hosting** → menu do site → **Configurações de liberação**, deixe **10 versões**.

#### 6. GitHub (nos dois repositórios)

As três variáveis, que não são segredos:

```powershell
$Provider = gcloud iam workload-identity-pools providers describe github-oidc --location=global --workload-identity-pool=github --format='value(name)'
foreach ($repo in 'RafaelMF16/LifeManager', 'RafaelMF16/LifeManagerFront') {
    gh variable set GCP_PROJECT_ID --repo $repo --body $P
    gh variable set GCP_WIF_PROVIDER --repo $repo --body $Provider
    gh variable set GCP_DEPLOYER_SA --repo $repo --body $D
}
```

E o Environment `production`, que só aceita deploy da `main`. Pela tela: **Settings → Environments → New environment** `production` → **Deployment branches and tags** → **Selected branches and tags** → `main`. Ou pelo `gh`:

```powershell
foreach ($repo in 'RafaelMF16/LifeManager', 'RafaelMF16/LifeManagerFront') {
    gh api -X PUT "repos/$repo/environments/production" -F 'deployment_branch_policy[protected_branches]=false' -F 'deployment_branch_policy[custom_branch_policies]=true'
    gh api -X POST "repos/$repo/environments/production/deployment-branch-policies" -f name=main -f type=branch
}
```

Sem a `gh`, as variáveis ficam em **Settings → Secrets and variables → Actions → Variables**.

### Reexecutar

GitHub → **Actions** → **Deploy** → **Run workflow** (na `main`). Serve para republicar sem um merge novo, por exemplo depois de corrigir uma permissão. Ou pelo terminal: `gh workflow run deploy.yml --repo RafaelMF16/LifeManager`.

### Rollback

- **API:** volte o tráfego para a revisão anterior. A imagem dela precisa ser uma das 3 que a limpeza mantém.
  ```powershell
  gcloud run revisions list --service=lifemanager-api --region=$R
  gcloud run services update-traffic lifemanager-api --region=$R --to-revisions=<revisão anterior>=100
  ```
  O próximo deploy volta a mandar todo o tráfego para a revisão nova. Também dá para reverter o merge no GitHub, o que gera um deploy novo. As migrations não são desfeitas no rollback: como são aditivas, a revisão anterior funciona com o schema novo.
- **Front:** Console do Firebase → **Hosting** → histórico de versões → **Reverter** na versão anterior.

## Operação

- **Logs:** console → Cloud Run → `lifemanager-api` ou `lifemanager-jobs` → Logs. Ou `gcloud run services logs read lifemanager-api --region=$R`.
- **Execuções dos jobs:** `gcloud run jobs executions list --job=lifemanager-jobs --region=$R` (ou `--job=lifemanager-migrate` para as migrations de cada deploy).
- **Imagens antigas:** a política de limpeza mantém as 3 mais recentes (veja [Limpeza das imagens](#4-limpeza-das-imagens)). O nível gratuito é de 0,5 GB, e cada imagem tem ~100–200 MB.
- **Backup manual:** `gcloud sql backups create --instance=lifemanager-db`.
- **Fim do Free Trial (90 dias):** faça o upgrade para conta paga (o custo segue em ~US$ 15/mês) ou exporte o banco antes de desligar:
  ```powershell
  gcloud sql export sql lifemanager-db gs://<bucket>/lifemanager.sql --database=lifemanager
  ```
  Sem o upgrade, os recursos param no fim do trial e os dados podem ser apagados 30 dias depois.
