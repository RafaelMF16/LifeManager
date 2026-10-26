<#
.SYNOPSIS
    Builds the API image, applies the pending migrations (job lifemanager-migrate) and deploys the image to Cloud Run: the
    service (lifemanager-api) and the hourly job (lifemanager-jobs).

.DESCRIPTION
    Needs the one-time setup in docs/deploy.md (Artifact Registry, Cloud SQL, secrets, service accounts).
    If the migrations fail, the script stops before the service and the hourly job change, so they keep the previous image.
.EXAMPLE
    ./deploy/deploy-api.ps1 -ProjectId lifemanager-prod
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectId,

    [string] $Region = 'southamerica-east1',

    # Where the links in the auth e-mails point: the Firebase Hosting site that serves the frontend.
    [string] $FrontendBaseUrl = "https://$ProjectId.web.app"
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked([scriptblock] $Command) {
    # gcloud writes its progress to stderr, which Windows PowerShell 5.1 turns into a terminating error under 'Stop'.
    # Native commands are checked by their exit code instead.
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$commit = (git -C $repoRoot rev-parse --short HEAD)
$tag = "$commit-$(Get-Date -Format 'yyyyMMddHHmmss')"
$image = "$Region-docker.pkg.dev/$ProjectId/lifemanager/api:$tag"
$sqlInstance = "${ProjectId}:${Region}:lifemanager-db"
$serviceAccount = "lifemanager-run@$ProjectId.iam.gserviceaccount.com"
# The service and both jobs build the same services at startup (AddApiServices), so all of them get every key, e-mail
# settings included. The sender address is a secret only to keep it out of this public repository.
$secrets = 'lifeManagerConnectionString=lifeManagerConnectionString:latest,accessTokenSecretKey=accessTokenSecretKey:latest,refreshTokenSecretKey=refreshTokenSecretKey:latest,brevoApiKey=brevoApiKey:latest,emailSenderAddress=emailSenderAddress:latest'
$envVars = "frontendBaseUrl=$FrontendBaseUrl"

if (git -C $repoRoot status --porcelain) {
    Write-Warning 'The working tree has uncommitted changes; they will be deployed too.'
}

Write-Host "==> Building $image" -ForegroundColor Cyan
Push-Location $repoRoot
try {
    Invoke-Checked { gcloud builds submit --project $ProjectId --config cloudbuild.yaml --substitutions "_IMAGE=$image" . }
}
finally {
    Pop-Location
}

# Before the service: the previous revision keeps serving on the new schema until the new one takes over, which is why
# migrations must stay additive (docs/deploy.md, "Migrations").
Write-Host '==> Applying the migrations (job lifemanager-migrate)' -ForegroundColor Cyan
Invoke-Checked {
    gcloud run jobs deploy lifemanager-migrate `
        --project $ProjectId `
        --region $Region `
        --image $image `
        --args='--migrate' `
        --service-account $serviceAccount `
        --set-cloudsql-instances $sqlInstance `
        --set-secrets $secrets `
        --set-env-vars $envVars `
        --cpu 1 `
        --memory 512Mi `
        --max-retries 0 `
        --task-timeout 10m
}
Invoke-Checked { gcloud run jobs execute lifemanager-migrate --project $ProjectId --region $Region --wait }

Write-Host '==> Deploying the service lifemanager-api' -ForegroundColor Cyan
Invoke-Checked {
    gcloud run deploy lifemanager-api `
        --project $ProjectId `
        --region $Region `
        --image $image `
        --service-account $serviceAccount `
        --add-cloudsql-instances $sqlInstance `
        --set-secrets $secrets `
        --set-env-vars "backgroundJobs__enabled=false,$envVars" `
        --cpu 1 `
        --memory 512Mi `
        --min-instances 0 `
        --max-instances 3 `
        --allow-unauthenticated
}

Write-Host '==> Deploying the job lifemanager-jobs' -ForegroundColor Cyan
Invoke-Checked {
    gcloud run jobs deploy lifemanager-jobs `
        --project $ProjectId `
        --region $Region `
        --image $image `
        --args='--run-jobs' `
        --service-account $serviceAccount `
        --set-cloudsql-instances $sqlInstance `
        --set-secrets $secrets `
        --set-env-vars $envVars `
        --cpu 1 `
        --memory 512Mi `
        --max-retries 0 `
        --task-timeout 15m
}

Write-Host "==> Done: $image" -ForegroundColor Green
