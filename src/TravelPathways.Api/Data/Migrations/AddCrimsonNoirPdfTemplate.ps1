# Inserts or updates Client — Crimson Noir PDF template (does not remove other templates).
# Usage:
#   $env:PGPASSWORD = 'your-password'
#   pwsh AddCrimsonNoirPdfTemplate.ps1 -Database TravelPathways

param(
    [string]$DbHost = "localhost",
    [int]$Port = 5432,
    [string]$Database = "travelpathways",
    [string]$User = "postgres"
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$HtmlPath = (Join-Path $ScriptDir "..\DefaultHtmlTemplates\ClientPdfOptions\pdf-client-16-crimson-noir.html" | Resolve-Path).Path
$Html = [IO.File]::ReadAllText($HtmlPath)
$Key = "pdf-client-16-crimson-noir"
$Name = "Client — Crimson Noir"
$Desc = "Luxury red, black and white — bold hero band and crimson accents."
$Tag = '$tpdf16$'

if ($Html.Contains($Tag)) { throw "HTML contains dollar-quote delimiter $Tag" }

$psql = $env:PSQL_PATH
if ([string]::IsNullOrWhiteSpace($psql)) {
    $found = Get-Command psql -ErrorAction SilentlyContinue
    if ($found) { $psql = $found.Source }
    else {
        $pgBin = Get-ChildItem "C:\Program Files\PostgreSQL" -Recurse -Filter psql.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($pgBin) { $psql = $pgBin.FullName }
    }
}
if ([string]::IsNullOrWhiteSpace($psql)) { throw "psql not found. Set PSQL_PATH or install PostgreSQL client tools." }

$TempSql = [IO.Path]::GetTempFileName() + ".sql"
$Body = @"
INSERT INTO "PdfTemplates" ("Id", "Key", "Name", "Description", "IsSystem", "IsActive", "HtmlTemplate", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")
VALUES (gen_random_uuid(), '$Key', '$Name', '$Desc', FALSE, TRUE, 
"@ + $Tag + $Html + $Tag + @"
, NOW(), NOW(), FALSE, NULL)
ON CONFLICT ("Key") DO UPDATE SET
  "Name" = EXCLUDED."Name",
  "Description" = EXCLUDED."Description",
  "IsActive" = TRUE,
  "IsDeleted" = FALSE,
  "DeletedAtUtc" = NULL,
  "HtmlTemplate" = EXCLUDED."HtmlTemplate",
  "UpdatedAt" = NOW();
"@
[IO.File]::WriteAllText($TempSql, $Body, [Text.UTF8Encoding]::new($false))

try {
    & $psql -h $DbHost -p $Port -U $User -d $Database -f $TempSql
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE" }
    Write-Host "Added/updated PDF template: $Name ($Key)"
    Write-Host "Restart the API, then select it under Travel Agent → PDF template."
}
finally {
    Remove-Item -LiteralPath $TempSql -ErrorAction SilentlyContinue
}
