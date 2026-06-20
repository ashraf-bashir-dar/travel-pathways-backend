# Generates PostgreSQL INSERT/UPSERT for ClientPdfOptions/*.html into PdfTemplates.
# Run: pwsh ./Export-ClientPdfTemplatesToSql.ps1
# Output: Script_SeedClientPdfTemplates.sql

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$HtmlDir = Join-Path $ScriptDir "..\DefaultHtmlTemplates\ClientPdfOptions" | Resolve-Path
$OutFile = Join-Path $ScriptDir "Script_SeedClientPdfTemplates.sql"

$rows = @(
    @{
        File = "pdf-client-01-classic-gold.html"; Key = "pdf-client-01-classic-gold"
        Name = "Client — Classic Gold"
        Desc = "Midnight blue ribbon, champagne gold accents (premium baseline)."
    }
    @{
        File = "pdf-client-02-onyx-rose.html"; Key = "pdf-client-02-onyx-rose"
        Name = "Client — Onyx & Rose"
        Desc = "Charcoal and dusty rose gradient — soft luxury."
    }
    @{
        File = "pdf-client-03-teal-sand.html"; Key = "pdf-client-03-teal-sand"
        Name = "Client — Teal & Sand"
        Desc = "Ocean teal to warm sand — executive resort."
    }
    @{
        File = "pdf-client-04-ink-paper.html"; Key = "pdf-client-04-ink-paper"
        Name = "Client — Ink & Paper"
        Desc = "Minimal black-on-white editorial layout."
    }
    @{
        File = "pdf-client-05-burgundy-ivory.html"; Key = "pdf-client-05-burgundy-ivory"
        Name = "Client — Heritage Burgundy"
        Desc = "Burgundy and gold on ivory — formal stationery."
    }
    @{
        File = "pdf-client-06-emerald-copper.html"; Key = "pdf-client-06-emerald-copper"
        Name = "Client — Emerald & Copper"
        Desc = "Deep green and copper metal — nature-forward luxury."
    }
    @{
        File = "pdf-client-07-royal-silver.html"; Key = "pdf-client-07-royal-silver"
        Name = "Client — Royal Silver"
        Desc = "Regal blue with brushed silver — crown-stripe emphasis."
    }
    @{
        File = "pdf-client-08-lavender-graphite.html"; Key = "pdf-client-08-lavender-graphite"
        Name = "Client — Lavender & Graphite"
        Desc = "Soft violet on cool graphite — refined spa aesthetic."
    }
    @{
        File = "pdf-client-09-terracotta-sun.html"; Key = "pdf-client-09-terracotta-sun"
        Name = "Client — Terracotta Sun"
        Desc = "Warm earthen clay and sun gold — Mediterranean warmth."
    }
    @{
        File = "pdf-client-10-sapphire-platinum.html"; Key = "pdf-client-10-sapphire-platinum"
        Name = "Client — Sapphire & Platinum"
        Desc = "Jewel blue with platinum trim — high-contrast gala look."
    }
    @{
        File = "pdf-client-11-forest-brass.html"; Key = "pdf-client-11-forest-brass"
        Name = "Client — Forest Brass"
        Desc = "Evergreen depth with aged brass — lodge editorial."
    }
    @{
        File = "pdf-client-12-indigo-dusk.html"; Key = "pdf-client-12-indigo-dusk"
        Name = "Client — Indigo Dusk"
        Desc = "Twilight indigo with rose silver accents — evening travel."
    }
    @{
        File = "pdf-client-13-slate-cyan.html"; Key = "pdf-client-13-slate-cyan"
        Name = "Client — Slate & Cyan"
        Desc = "Cool slate with cyan spark — tech-clean itinerary."
    }
    @{
        File = "pdf-client-14-wine-parchment.html"; Key = "pdf-client-14-wine-parchment"
        Name = "Client — Wine & Parchment"
        Desc = "Oxblood on warm parchment — cellar invitation style."
    }
    @{
        File = "pdf-client-15-carbon-lime.html"; Key = "pdf-client-15-carbon-lime"
        Name = "Client — Carbon & Lime"
        Desc = "Near-black shell with electric lime ribbon — bold contrast."
    }
    @{
        File = "pdf-client-16-crimson-noir.html"; Key = "pdf-client-16-crimson-noir"
        Name = "Client — Crimson Noir"
        Desc = "Luxury red, black and white — bold hero band and crimson accents."
    }
)

# PostgreSQL dollar-quote tags ( $name$content$name ) — unique per row
$tags = @(
    '$tpdf01$', '$tpdf02$', '$tpdf03$', '$tpdf04$', '$tpdf05$',
    '$tpdf06$', '$tpdf07$', '$tpdf08$', '$tpdf09$', '$tpdf10$',
    '$tpdf11$', '$tpdf12$', '$tpdf13$', '$tpdf14$', '$tpdf15$', '$tpdf16$'
)

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("-- Seed / upsert client-selectable PDF HtmlTemplates.")
[void]$sb.AppendLine("-- Re-run safe: updates HtmlTemplate when Key matches.")
[void]$sb.AppendLine('-- Set tenant: UPDATE "Tenants" SET "PdfTemplateKey" = ''pdf-client-01-classic-gold'' WHERE ...')
[void]$sb.AppendLine("BEGIN;")
[void]$sb.AppendLine()

for ($i = 0; $i -lt $rows.Count; $i++) {
    $r = $rows[$i]
    $tag = $tags[$i]
    $path = Join-Path $HtmlDir $r.File
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    $html = [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false))
    $inner = $tag.Trim('$')
    if ($html.Contains($tag) -or $html.Contains("`$$inner`$")) { throw "HTML contains delimiter $tag" }

    $k = $r.Key.Replace("'", "''")
    $n = $r.Name.Replace("'", "''")
    $d = $r.Desc.Replace("'", "''")

    [void]$sb.AppendLine("-- $($r.Key)")
    [void]$sb.AppendLine('INSERT INTO "PdfTemplates" ("Id", "Key", "Name", "Description", "IsSystem", "IsActive", "HtmlTemplate", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")')
    [void]$sb.Append("VALUES (gen_random_uuid(), ")
    [void]$sb.Append("'$k', ")
    [void]$sb.Append("'$n', ")
    [void]$sb.Append("'$d', ")
    [void]$sb.AppendLine("FALSE, TRUE,")
    [void]$sb.AppendLine($tag)
    [void]$sb.AppendLine($html)
    [void]$sb.AppendLine($tag)
    [void]$sb.AppendLine(", NOW(), NOW(), FALSE, NULL)")
    [void]$sb.AppendLine("ON CONFLICT (""Key"") DO UPDATE SET")
    [void]$sb.AppendLine("  ""Name"" = EXCLUDED.""Name"",")
    [void]$sb.AppendLine("  ""Description"" = EXCLUDED.""Description"",")
    [void]$sb.AppendLine("  ""IsActive"" = EXCLUDED.""IsActive"",")
    [void]$sb.AppendLine("  ""HtmlTemplate"" = EXCLUDED.""HtmlTemplate"",")
    [void]$sb.AppendLine("  ""UpdatedAt"" = NOW();")
    [void]$sb.AppendLine()
}

[void]$sb.AppendLine("COMMIT;")
[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $OutFile ($([math]::Round((Get-Item $OutFile).Length / 1MB, 2)) MB)"
