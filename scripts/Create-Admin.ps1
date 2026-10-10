$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\backend\Millionnika.Api\Millionnika.Api.csproj'
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\millionnika-api-development\secrets.json'
if (-not (Test-Path -LiteralPath $secretPath)) {
    throw 'Database settings not found for this Windows user. Run from the Windows account used to set up the project.'
}
$settings = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$connection = $settings.'ConnectionStrings:GameDatabase'
if ([string]::IsNullOrWhiteSpace($connection)) {
    throw 'GameDatabase connection is missing from User Secrets.'
}
$previousConnection = $env:ConnectionStrings__GameDatabase
$previousEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:ConnectionStrings__GameDatabase = $connection
    $env:DOTNET_ENVIRONMENT = 'Development'
    & dotnet run --project $project -- --create-admin
    if ($LASTEXITCODE -ne 0) { throw 'Administrator creation failed.' }
}
finally {
    $env:ConnectionStrings__GameDatabase = $previousConnection
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
    $connection = $null
    $settings = $null
    $previousConnection = $null
}
