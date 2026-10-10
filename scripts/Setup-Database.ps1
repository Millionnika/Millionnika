[CmdletBinding()]
param(
    [string]$PostgreSqlBin = "D:\PostgreSQL\18\bin",
    [string]$AdminUser = "postgres"
)

$ErrorActionPreference = "Stop"

$psql = Join-Path $PostgreSqlBin "psql.exe"
$project = Join-Path $PSScriptRoot "..\backend\Millionnika.Api\Millionnika.Api.csproj"

if (-not (Test-Path -LiteralPath $psql)) {
    throw "psql.exe не найден. Укажите путь параметром -PostgreSqlBin."
}

$adminPassword = Read-Host "Пароль администратора PostgreSQL ($AdminUser)" -AsSecureString
$adminCredential = [System.Net.NetworkCredential]::new("", $adminPassword)
$appPasswordBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
$appPassword = [Convert]::ToBase64String($appPasswordBytes).Replace("+", "-").Replace("/", "_").TrimEnd("=")
$escapedAppPassword = $appPassword.Replace("'", "''")

try {
    $env:PGPASSWORD = $adminCredential.Password

    $roleExists = & $psql -X -q -t -A -w -h localhost -p 5432 -U $AdminUser -d postgres -c "SELECT 1 FROM pg_roles WHERE rolname = 'millionnika_app';"
    if ($LASTEXITCODE -ne 0) { throw "Не удалось подключиться к PostgreSQL с учётной записью администратора." }

    if ($roleExists -ne "1") {
        & $psql -X -q -w -h localhost -p 5432 -U $AdminUser -d postgres -c "CREATE ROLE millionnika_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '$escapedAppPassword';"
    } else {
        & $psql -X -q -w -h localhost -p 5432 -U $AdminUser -d postgres -c "ALTER ROLE millionnika_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '$escapedAppPassword';"
    }
    if ($LASTEXITCODE -ne 0) { throw "Не удалось настроить пользователя millionnika_app." }

    $databaseExists = & $psql -X -q -t -A -w -h localhost -p 5432 -U $AdminUser -d postgres -c "SELECT 1 FROM pg_database WHERE datname = 'millionnika';"
    if ($databaseExists -ne "1") {
        & $psql -X -q -w -h localhost -p 5432 -U $AdminUser -d postgres -c "CREATE DATABASE millionnika OWNER millionnika_app;"
        if ($LASTEXITCODE -ne 0) { throw "Не удалось создать базу millionnika." }
    }

    & $psql -X -q -w -h localhost -p 5432 -U $AdminUser -d postgres -c "REVOKE ALL ON DATABASE millionnika FROM PUBLIC; GRANT CONNECT, TEMPORARY ON DATABASE millionnika TO millionnika_app;"
    & $psql -X -q -w -h localhost -p 5432 -U $AdminUser -d millionnika -c "REVOKE CREATE ON SCHEMA public FROM PUBLIC; GRANT USAGE, CREATE ON SCHEMA public TO millionnika_app;"
    if ($LASTEXITCODE -ne 0) { throw "Не удалось применить ограниченные права." }

    $env:PGPASSWORD = $appPassword
    & $psql -X -q -t -A -w -h localhost -p 5432 -U millionnika_app -d millionnika -c "SELECT 1;" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Проверка подключения пользователя игры завершилась ошибкой." }

    $connectionString = "Host=localhost;Port=5432;Database=millionnika;Username=millionnika_app;Password=$appPassword;Timeout=3;Command Timeout=3"
    & dotnet user-secrets set "ConnectionStrings:GameDatabase" $connectionString --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Не удалось сохранить строку подключения в User Secrets." }

    Write-Host "Готово: база и отдельный пользователь созданы, подключение проверено."
    Write-Host "Пароль приложения сохранён только в .NET User Secrets и не выведен на экран."
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $adminCredential.Password = ""
    $appPassword = ""
    $escapedAppPassword = ""
}
