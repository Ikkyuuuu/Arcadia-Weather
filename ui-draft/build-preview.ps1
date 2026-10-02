# Make a single, portable HTML preview from the editable source files.
$ErrorActionPreference = 'Stop'
$draftRoot = $PSScriptRoot
$html = [IO.File]::ReadAllText((Join-Path $draftRoot 'index.html'))
$styles = [IO.File]::ReadAllText((Join-Path $draftRoot 'styles.css'))
$script = [IO.File]::ReadAllText((Join-Path $draftRoot 'app.js'))
$html = $html.Replace('<link rel="stylesheet" href="styles.css">', "<style>`n$styles`n</style>")
# The inline script waits for the page to exist, just like the deferred source.
$html = $html.Replace('<script src="app.js" defer></script>', '')
$html = $html.Replace('</body>', "<script>`n$script`n</script>`n</body>")
foreach ($number in 1..5) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $draftRoot "assets\day$number.jpg"))
    $dataUri = 'data:image/jpeg;base64,' + [Convert]::ToBase64String($bytes)
    $html = $html.Replace("assets/day$number.jpg", $dataUri)
}
$outputPath = Join-Path $draftRoot 'Arcadia-preview.html'
[IO.File]::WriteAllText($outputPath, $html, [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $outputPath | Select-Object FullName, Length
