# One-time theme application: pdf-client-06 .. 15 copied from premium — applies distinct :root, ribbon, shell, pricing bar.
# Re-run only if those files are reset from premium.
$ErrorActionPreference = "Stop"
$dir = $PSScriptRoot

$themes = [ordered]@{
    "pdf-client-06-emerald-copper.html" = @{
        Meta = "Client PDF Option 6 — Emerald canopy &amp; copper accents"
        Title = "Travel Itinerary — Emerald Copper"
        Blurb = "OPTION 6 — Organic luxury: deep emerald, burnished copper, mint-wash page."
        Root = @'
    :root {
      --tp-primary: #064e3b;
      --tp-secondary: #b45309;
      --ink: #14532d;
      --muted: #57645c;
      --line: #d8ebe4;
      --surface: #f0fdf4;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #0f766e;
      --shadow: 0 4px 22px rgba(6, 78, 59, 0.09);
      --shadow-tight: 0 2px 8px rgba(6, 78, 59, 0.07);
      --glow-gold: linear-gradient(135deg, rgba(180, 83, 9, 0.18) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #ecfdf5 0%, #ffffff 150px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0 0 22px 22px;
      box-shadow: 0 14px 44px rgba(6, 78, 59, 0.1);
    }
    .ribbon {
      height: 14px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.4) 0%, rgba(255, 255, 255, 0) 55%),
        linear-gradient(90deg, #022c22 0%, #047857 32%, #059669 58%, #b45309 85%, #fbbf24 100%);
      border-radius: 0 0 16px 16px;
      box-shadow: 0 6px 18px rgba(6, 78, 59, 0.14);
    }
'@
        Pricing = "linear-gradient(118deg, #022c22 0%, #065f46 38%, #b45309 78%, #fcd34d 100%)"
    }
    "pdf-client-07-royal-silver.html" = @{
        Meta = "Client PDF Option 7 — Royal navy &amp; silver executive"
        Title = "Travel Itinerary — Royal Silver"
        Blurb = "OPTION 7 — Corporate blue ribbon, cool silver, crisp glass panels."
        Root = @'
    :root {
      --tp-primary: #1e3a8a;
      --tp-secondary: #94a3b8;
      --ink: #0f172a;
      --muted: #64748b;
      --line: #e2e8f0;
      --surface: #f8fafc;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #1d4ed8;
      --shadow: 0 4px 20px rgba(30, 58, 138, 0.1);
      --shadow-tight: 0 2px 6px rgba(30, 58, 138, 0.08);
      --glow-gold: linear-gradient(135deg, rgba(148, 163, 184, 0.2) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #eff6ff 0%, #ffffff 140px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0 0 12px 12px;
      box-shadow: 0 8px 36px rgba(30, 58, 138, 0.11);
      border: 1px solid #e2e8f0;
      border-top: none;
    }
    .ribbon {
      height: 11px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.45) 0%, rgba(255, 255, 255, 0) 50%),
        linear-gradient(90deg, #172554 0%, #1d4ed8 45%, #cbd5e1 82%, #f1f5f9 100%);
      border-radius: 0 0 10px 10px;
      box-shadow: 0 4px 16px rgba(30, 58, 138, 0.12);
    }
'@
        Pricing = "linear-gradient(118deg, #172554 0%, #2563eb 50%, #94a3b8 92%, #e2e8f0 100%)"
    }
    "pdf-client-08-lavender-graphite.html" = @{
        Meta = "Client PDF Option 8 — Lavender mist &amp; graphite"
        Title = "Travel Itinerary — Lavender Graphite"
        Blurb = "OPTION 8 — Soft violet accents, graphite typography, rounded editorial shell."
        Root = @'
    :root {
      --tp-primary: #1f2937;
      --tp-secondary: #7c3aed;
      --ink: #111827;
      --muted: #6b7280;
      --line: #e9e3f4;
      --surface: #faf5ff;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #6d28d9;
      --shadow: 0 6px 28px rgba(124, 58, 237, 0.08);
      --shadow-tight: 0 2px 8px rgba(31, 41, 55, 0.06);
      --glow-gold: linear-gradient(135deg, rgba(124, 58, 237, 0.15) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #f5f3ff 0%, #ffffff 180px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 6px 24px 40px;
      background: #fff;
      border-radius: 0 0 26px 26px;
      box-shadow: 0 16px 50px rgba(124, 58, 237, 0.09);
    }
    .ribbon {
      height: 13px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.5) 0%, transparent 55%),
        linear-gradient(90deg, #1f2937 0%, #4c1d95 40%, #a78bfa 75%, #e9d5ff 100%);
      border-radius: 0 0 20px 20px;
      box-shadow: 0 8px 24px rgba(76, 29, 149, 0.1);
    }
'@
        Pricing = "linear-gradient(118deg, #111827 0%, #5b21b6 45%, #8b5cf6 80%, #e9d5ff 100%)"
    }
    "pdf-client-09-terracotta-sun.html" = @{
        Meta = "Client PDF Option 9 — Terracotta &amp; sun-washed cream"
        Title = "Travel Itinerary — Terracotta Sun"
        Blurb = "OPTION 9 — Mediterranean warmth, clay and amber, sunlit paper."
        Root = @'
    :root {
      --tp-primary: #9a3412;
      --tp-secondary: #ea580c;
      --ink: #422006;
      --muted: #78716c;
      --line: #ecdcc8;
      --surface: #fffbeb;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #c2410c;
      --shadow: 0 4px 22px rgba(154, 52, 18, 0.1);
      --shadow-tight: 0 2px 8px rgba(154, 52, 18, 0.07);
      --glow-gold: linear-gradient(135deg, rgba(234, 88, 12, 0.18) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #fff7ed 0%, #ffffff 150px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fffefb;
      border-radius: 0 0 16px 16px;
      box-shadow: 0 12px 40px rgba(154, 52, 18, 0.09);
    }
    .ribbon {
      height: 13px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.35) 0%, transparent 55%),
        linear-gradient(90deg, #7c2d12 0%, #c2410c 35%, #fb923c 72%, #fde68a 100%);
      border-radius: 0 0 14px 14px;
      box-shadow: 0 6px 20px rgba(194, 65, 12, 0.14);
    }
'@
        Pricing = "linear-gradient(118deg, #431407 0%, #9a3412 38%, #ea580c 72%, #fbbf24 100%)"
    }
    "pdf-client-10-sapphire-platinum.html" = @{
        Meta = "Client PDF Option 10 — Sapphire &amp; platinum"
        Title = "Travel Itinerary — Sapphire Platinum"
        Blurb = "OPTION 10 — Jewellery-luxury: saturated sapphire, bright platinum highlights."
        Root = @'
    :root {
      --tp-primary: #1e40af;
      --tp-secondary: #cbd5e1;
      --ink: #0f172a;
      --muted: #64748b;
      --line: #e2e8f0;
      --surface: #f8fafc;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #2563eb;
      --shadow: 0 4px 26px rgba(30, 64, 175, 0.11);
      --shadow-tight: 0 2px 8px rgba(30, 64, 175, 0.08);
      --glow-gold: linear-gradient(135deg, rgba(203, 213, 225, 0.35) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #eff6ff 0%, #ffffff 130px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0 0 10px 10px;
      box-shadow: 0 10px 42px rgba(30, 64, 175, 0.1);
      border: 1px solid #e0e7ff;
      border-top: none;
    }
    .ribbon {
      height: 12px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.5) 0%, transparent 50%),
        linear-gradient(90deg, #1e3a8a 0%, #2563eb 38%, #93c5fd 70%, #f1f5f9 100%);
      border-radius: 0 0 8px 8px;
      box-shadow: 0 6px 22px rgba(37, 99, 235, 0.15);
    }
'@
        Pricing = "linear-gradient(118deg, #172554 0%, #1d4ed8 45%, #e2e8f0 88%, #f8fafc 100%)"
    }
    "pdf-client-11-forest-brass.html" = @{
        Meta = "Client PDF Option 11 — Deep forest &amp; antique brass"
        Title = "Travel Itinerary — Forest Brass"
        Blurb = "OPTION 11 — Lodge aesthetic: pine greens, hammered brass, kraft undertone."
        Root = @'
    :root {
      --tp-primary: #14532d;
      --tp-secondary: #a16207;
      --ink: #1c1917;
      --muted: #57534e;
      --line: #d6d3d1;
      --surface: #f5f5f4;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #15803d;
      --shadow: 0 4px 22px rgba(20, 83, 45, 0.09);
      --shadow-tight: 0 2px 6px rgba(20, 83, 45, 0.07);
      --glow-gold: linear-gradient(135deg, rgba(161, 98, 7, 0.2) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #ecfccb 0%, #ffffff 140px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fffdf8;
      border-radius: 0 0 18px 18px;
      box-shadow: 0 12px 38px rgba(20, 83, 45, 0.09);
    }
    .ribbon {
      height: 12px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.3) 0%, transparent 55%),
        linear-gradient(90deg, #052e16 0%, #166534 40%, #a16207 78%, #fde68a 100%);
      border-radius: 0 0 14px 14px;
      box-shadow: 0 6px 18px rgba(22, 101, 52, 0.13);
    }
'@
        Pricing = "linear-gradient(118deg, #052e16 0%, #166534 42%, #a16207 80%, #fde047 100%)"
    }
    "pdf-client-12-indigo-dusk.html" = @{
        Meta = "Client PDF Option 12 — Indigo twilight &amp; sunset peach"
        Title = "Travel Itinerary — Indigo Dusk"
        Blurb = "OPTION 12 — Twilight palette: indigo night sky melting into soft peach."
        Root = @'
    :root {
      --tp-primary: #312e81;
      --tp-secondary: #fdba74;
      --ink: #1e1b4b;
      --muted: #6366f1;
      --line: #e0e7ff;
      --surface: #eef2ff;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #4f46e5;
      --shadow: 0 6px 26px rgba(49, 46, 129, 0.1);
      --shadow-tight: 0 2px 8px rgba(49, 46, 129, 0.08);
      --glow-gold: linear-gradient(135deg, rgba(253, 186, 116, 0.25) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #eef2ff 0%, #fff7ed 45%, #ffffff 160px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0 0 20px 20px;
      box-shadow: 0 14px 48px rgba(49, 46, 129, 0.11);
    }
    .ribbon {
      height: 15px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.35) 0%, transparent 50%),
        linear-gradient(90deg, #1e1b4b 0%, #4338ca 30%, #fdba74 72%, #ffedd5 100%);
      border-radius: 0 0 18px 18px;
      box-shadow: 0 8px 28px rgba(67, 56, 202, 0.14);
    }
'@
        Pricing = "linear-gradient(118deg, #1e1b4b 0%, #4338ca 45%, #fb923c 85%, #ffedd5 100%)"
    }
    "pdf-client-13-slate-cyan.html" = @{
        Meta = "Client PDF Option 13 — Slate &amp; cyan pulse"
        Title = "Travel Itinerary — Slate Cyan"
        Blurb = "OPTION 13 — Tech-forward travel: cool slate panels, electric cyan accent."
        Root = @'
    :root {
      --tp-primary: #334155;
      --tp-secondary: #06b6d4;
      --ink: #0f172a;
      --muted: #64748b;
      --line: #e2e8f0;
      --surface: #f1f5f9;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #0891b2;
      --shadow: 0 2px 18px rgba(51, 65, 85, 0.1);
      --shadow-tight: 0 1px 4px rgba(51, 65, 85, 0.08);
      --glow-gold: linear-gradient(135deg, rgba(6, 182, 212, 0.15) 0%, transparent 100%);
    }
'@
        BodyBg = "#f8fafc"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0;
      box-shadow: 0 1px 0 #e2e8f0, 0 8px 28px rgba(15, 23, 42, 0.06);
      border: 1px solid #e2e8f0;
      border-top: none;
    }
    .ribbon {
      height: 6px;
      margin-bottom: 0;
      background: linear-gradient(90deg, #0e7490 0%, #06b6d4 40%, #334155 100%);
      border-radius: 0;
      box-shadow: none;
    }
'@
        Pricing = "linear-gradient(90deg, #334155 0%, #06b6d4 55%, #0e7490 100%)"
    }
    "pdf-client-14-wine-parchment.html" = @{
        Meta = "Client PDF Option 14 — Cellar wine &amp; parchment"
        Title = "Travel Itinerary — Wine Parchment"
        Blurb = "OPTION 14 — Winery stationery: oxblood, dusty rose, aged parchment."
        Root = @'
    :root {
      --tp-primary: #7f1d1d;
      --tp-secondary: #be123c;
      --ink: #292524;
      --muted: #78716c;
      --line: #e7e5e4;
      --surface: #fafaf9;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #991b1b;
      --shadow: 0 4px 22px rgba(127, 29, 29, 0.1);
      --shadow-tight: 0 2px 6px rgba(127, 29, 29, 0.07);
      --glow-gold: linear-gradient(135deg, rgba(190, 18, 60, 0.12) 0%, transparent 100%);
    }
'@
        BodyBg = "linear-gradient(180deg, #fafaf9 0%, #ffffff 120px)"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fffef8;
      border-radius: 0 0 14px 14px;
      box-shadow: 0 10px 36px rgba(127, 29, 29, 0.08);
      border: 1px solid #f5e6e0;
      border-top: none;
    }
    .ribbon {
      height: 12px;
      margin-bottom: 0;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.25) 0%, transparent 55%),
        linear-gradient(90deg, #450a0a 0%, #7f1d1d 35%, #be123c 70%, #fecdd3 100%);
      border-radius: 0 0 12px 12px;
      box-shadow: 0 6px 16px rgba(127, 29, 29, 0.12);
    }
'@
        Pricing = "linear-gradient(118deg, #291011 0%, #7f1d1d 40%, #9f1239 75%, #fecdd3 100%)"
    }
    "pdf-client-15-carbon-lime.html" = @{
        Meta = "Client PDF Option 15 — Carbon &amp; electric lime"
        Title = "Travel Itinerary — Carbon Lime"
        Blurb = "OPTION 15 — Bold contemporary: near-black shell, neon lime accent stripe."
        Root = @'
    :root {
      --tp-primary: #0a0a0a;
      --tp-secondary: #84cc16;
      --ink: #171717;
      --muted: #737373;
      --line: #e5e5e5;
      --surface: #fafafa;
      --primary: var(--tp-primary);
      --secondary: var(--tp-secondary);
      --quote-amount: #65a30d;
      --shadow: 0 4px 20px rgba(0, 0, 0, 0.12);
      --shadow-tight: 0 1px 4px rgba(0, 0, 0, 0.1);
      --glow-gold: linear-gradient(135deg, rgba(132, 204, 22, 0.2) 0%, transparent 100%);
    }
'@
        BodyBg = "#fafafa"
        DocShell = @'
    .doc-shell {
      max-width: 720px;
      margin: 0 auto;
      padding: 4px 22px 36px;
      background: #fff;
      border-radius: 0 0 8px 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.14);
      border: 1px solid #e5e5e5;
      border-top: none;
    }
    .ribbon {
      height: 5px;
      margin-bottom: 0;
      background: linear-gradient(90deg, #000000 0%, #84cc16 45%, #000000 100%);
      border-radius: 0;
      box-shadow: 0 0 12px rgba(132, 204, 22, 0.35);
    }
'@
        Pricing = "linear-gradient(90deg, #0a0a0a 0%, #3f6212 40%, #84cc16 75%, #171717 100%)"
    }
}

$headPattern = '(?s)  <meta name="description" content="[^"]*" />\r?\n  <title>[^<]*</title>\r?\n  <style>\r?\n    /\*.*?\*/\r?\n    :root \{.*?\n    \}\r?\n    \* \{ box-sizing: border-box; \}\r?\n    html \{.*?\n    \}\r?\n    body \{.*?\n      background: [^;]+;\r?\n    \}\r?\n\r?\n    /\* --- Top ribbon --- \*/\r?\n    \.doc-shell \{.*?\n    \}\r?\n    \.ribbon \{.*?\n    \}'

foreach ($kv in $themes.GetEnumerator()) {
    $fn = $kv.Key
    $t = $kv.Value
    $path = Join-Path $dir $fn
    $raw = Get-Content -Path $path -Raw -Encoding UTF8

    $comment = @"
    /*
      CLIENT PDF $($t.Blurb)
      Same tokens as professional-package-pdf-premium.html.
    */
"@
    $replacement = @"
  <meta name="description" content="$($t.Meta)" />
  <title>$($t.Title)</title>
  <style>
$comment
$($t.Root.TrimEnd())

    * { box-sizing: border-box; }
    html {
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    body {
      margin: 0;
      padding: 0 0 32px;
      font-family: "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif;
      font-size: 10.5pt;
      line-height: 1.55;
      color: var(--ink);
      background: $($t.BodyBg);
    }

    /* --- Top ribbon --- */
$($t.DocShell.TrimEnd())
"@

    $new = [regex]::Replace($raw, $headPattern, $replacement.TrimEnd(), 1)
    if ($new -eq $raw) { throw "Pattern failed for $fn" }

    $new = $new.Replace(
        'background: linear-gradient(118deg, #050a14 0%, #152642 42%, #264a7a 68%, #9a7419 92%, #d4b84a 100%);',
        "background: $($t.Pricing);"
    )
    if (-not $new.Contains($t.Pricing)) { throw "Pricing replace failed for $fn" }

    Set-Content -Path $path -Value $new -Encoding utf8 -NoNewline
    Write-Host "OK $fn"
}

Write-Host "Done. Re-run Export-ClientPdfTemplatesToSql.ps1 to regenerate SQL."
