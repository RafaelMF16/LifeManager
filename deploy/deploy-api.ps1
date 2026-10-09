<#
.SYNOPSIS
    Builds the API image and deploys it to Cloud Run: the service (lifemanager-api) and the hourly job (lifemanager-jobs).

.DESCRIPTION
    Needs the one-time setup in docs/deploy.md (Artifact Registry, Cloud SQL, secrets, service accounts).
    Run pending migrations BEFORE this script (docs/deploy.md, "Migrations").

.EXAMPLE
    ./deploy/deploy-api.ps1 -ProjectId lifemanager-prod
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectId,

    [string] $Region = 'southamerica-east1'
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked([scriptblock] $Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$commit = (git -C $repoRoot rev-parse --short HEAD)
$tag = "$commit-$(Get-Date -Format 'yyyyMMddHHmmss')"
$image = "$Region-docker.pkg.dev/$ProjectId/lifemanager/api:$tag"
$sqlInstance = "${ProjectId}:${Region}:lifemanager-db"
$serviceAccount = "lifemanager-run@$ProjectId.iam.gserviceaccount.com"
$secrets = 'lifeManagerConnectionString=lifeManagerConnectionString:latest,accessTokenSecretKey=accessTokenSecretKey:latest,refreshTokenSecretKey=refreshTokenSecretKey:latest'

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

Write-Host '==> Deploying the service lifemanager-api' -ForegroundColor Cyan
Invoke-Checked {
    gcloud run deploy lifemanager-api `
        --project $ProjectId `
        --region $Region `
        --image $image `
        --service-account $serviceAccount `
        --add-cloudsql-instances $sqlInstance `
        --set-secrets $secrets `
        --set-env-vars 'backgroundJobs__enabled=false' `
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
        --cpu 1 `
        --memory 512Mi `
        --max-retries 0 `
        --task-timeout 15m
}

Write-Host "==> Done: $image" -ForegroundColor Green
