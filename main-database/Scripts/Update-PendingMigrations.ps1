<#
.SYNOPSIS
    Checks the EF Core model for db-model/main-database against the last migration,
    and scaffolds a new migration (named after the current timestamp) if they differ.

.DESCRIPTION
    Runs `dotnet ef migrations has-pending-model-changes`. If it reports drift,
    runs `dotnet ef migrations add <yyyyMMddHHmmss>`. Never touches the database
    itself - `dotnet ef migrations add` only writes files under Migrations/.

    Intended to run automatically before building/running api-server (see the
    EnsureMigrations target in api-server.csproj). Auto-generated migrations are
    a dev-time convenience, not a substitute for reviewing the diff - check the
    generated file before committing it.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot '..'

Push-Location $projectDir
try {
    Write-Host "Checking for pending EF Core model changes (main-database)..."

    $output = & dotnet ef migrations has-pending-model-changes --project . --startup-project . 2>&1
    $exitCode = $LASTEXITCODE
    $outputText = ($output | Out-String)

    if ($exitCode -eq 0) {
        Write-Host "No pending model changes."
        exit 0
    }

    if ($outputText -notmatch 'Changes have been made to the model since the last migration') {
        Write-Host $outputText
        throw "dotnet ef migrations has-pending-model-changes failed unexpectedly - not a plain pending-changes result. See output above."
    }

    $migrationName = Get-Date -Format 'yyyyMMddHHmmss'
    Write-Host "Pending model changes detected. Creating migration '$migrationName'..."

    & dotnet ef migrations add $migrationName --project . --startup-project .
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations add failed for '$migrationName'."
    }

    Write-Host "Migration '$migrationName' created under Migrations/. Review it before committing."
}
finally {
    Pop-Location
}
