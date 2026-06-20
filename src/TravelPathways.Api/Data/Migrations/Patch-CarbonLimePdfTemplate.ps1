# Updates the live PdfTemplates row for Client — Carbon & Lime from pdf-client-15-carbon-lime.html
# Usage (PostgreSQL):
#   $env:PGPASSWORD = 'your-password'
#   pwsh Patch-CarbonLimePdfTemplate.ps1 -DbHost localhost -Port 5432 -Database TravelPathways -User postgres

param(
    [string]$DbHost = "localhost",
    [int]$Port = 5432,
    [string]$Database = "travelpathways",
    [string]$User = "postgres"
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$HtmlPath = (Join-Path $ScriptDir "..\DefaultHtmlTemplates\ClientPdfOptions\pdf-client-15-carbon-lime.html" | Resolve-Path).Path
$Html = [IO.File]::ReadAllText($HtmlPath)
$Key = "pdf-client-15-carbon-lime"

$TempSql = [IO.Path]::GetTempFileName() + ".sql"
$Body = @"
UPDATE "PdfTemplates"
SET "HtmlTemplate" = `$html`$Html`$html,
    "UpdatedAt" = NOW()
WHERE "Key" = '$Key' AND NOT "IsDeleted";
"@
[IO.File]::WriteAllText($TempSql, $Body, [Text.UTF8Encoding]::new($false))

try {
    & psql -h $DbHost -p $Port -U $User -d $Database -f $TempSql
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE" }
    Write-Host "Updated PdfTemplates for key: $Key"
}
finally {
    Remove-Item -LiteralPath $TempSql -ErrorAction SilentlyContinue
}
