# 1) Upsert Client — Carbon & Lime HTML from source file
# 2) Soft-delete all other PdfTemplates
# 3) Set all tenants to pdf-client-15-carbon-lime
#
# Usage:
#   $env:PGPASSWORD = 'your-password'
#   pwsh KeepOnlyCarbonLimePdfTemplate.ps1 -DbHost localhost -Port 5432 -Database TravelPathways -User postgres

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
$Name = "Client — Carbon & Lime"
$Desc = "Near-black shell with electric lime ribbon — bold contrast."
$DollarTag = "tpdf15"

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
BEGIN;

INSERT INTO "PdfTemplates" ("Id", "Key", "Name", "Description", "IsSystem", "IsActive", "HtmlTemplate", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")
VALUES (gen_random_uuid(), '$Key', '$Name', '$Desc', FALSE, TRUE, `$$DollarTag`$Html`$$DollarTag`, NOW(), NOW(), FALSE, NULL)
ON CONFLICT ("Key") DO UPDATE SET
  "Name" = EXCLUDED."Name",
  "Description" = EXCLUDED."Description",
  "IsActive" = TRUE,
  "IsDeleted" = FALSE,
  "DeletedAtUtc" = NULL,
  "HtmlTemplate" = EXCLUDED."HtmlTemplate",
  "UpdatedAt" = NOW();

UPDATE "Tenants"
SET "PdfTemplateKey" = '$Key'
WHERE "PdfTemplateKey" IS DISTINCT FROM '$Key';

UPDATE "PdfTemplates"
SET "IsDeleted" = TRUE, "DeletedAtUtc" = NOW(), "IsActive" = FALSE, "UpdatedAt" = NOW()
WHERE "Key" <> '$Key' AND "IsDeleted" = FALSE;

COMMIT;
"@
[IO.File]::WriteAllText($TempSql, $Body, [Text.UTF8Encoding]::new($false))

try {
    & $psql -h $DbHost -p $Port -U $User -d $Database -f $TempSql
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE" }
    Write-Host "Done. Only '$Key' remains active. All tenants now use Carbon & Lime."
    Write-Host "Restart the API to clear the PDF template cache."
}
finally {
    Remove-Item -LiteralPath $TempSql -ErrorAction SilentlyContinue
}
