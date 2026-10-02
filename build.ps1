param(
    [switch]$SkipTests,
    [string]$VideoSource,
    [string]$OutputDirectory,
    [switch]$CreateArchive,
    [switch]$SingleExe
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localSdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
[xml]$project = Get-Content -LiteralPath 'src\ArcadiaWeather\ArcadiaWeather.csproj' -Raw
$version = $project.Project.PropertyGroup.Version
if (-not $OutputDirectory) {
    $folderName = if ($SingleExe) { "ArcadiaWeather-$version-single" } else { "ArcadiaWeather-$version" }
    $OutputDirectory = Join-Path $PSScriptRoot "dist\$folderName"
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory, $PSScriptRoot)
$mediaOutput = Join-Path $outputPath 'Media'
if (-not $VideoSource) {
    $VideoSource = if (Test-Path -LiteralPath $mediaOutput) { $mediaOutput } else { Join-Path $PSScriptRoot 'Media' }
}
$sourcePath = (Resolve-Path -LiteralPath $VideoSource).Path
$videoNames = @(1..5 | ForEach-Object { "Arcadia Bay Day $_ Sound AAC.mp4" })
foreach ($name in $videoNames) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $name) -PathType Leaf)) {
        throw "Missing build input: $name. Supply the five source videos with -VideoSource."
    }
}
$outputExe = Join-Path $outputPath 'ArcadiaWeather.exe'
$activeCopy = Get-Process -Name ArcadiaWeather -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $outputExe }
if ($activeCopy) { throw 'The output app is running. Close it or choose another -OutputDirectory.' }
if (-not $SkipTests) {
    & $sdk run --project 'tests\ArcadiaWeather.Tests' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $sdk run --project 'tests\ArcadiaWeather.Preferences.Tests' -c Release -- (Join-Path $PSScriptRoot 'artifacts\theme-preference-tests')
    if ($LASTEXITCODE -ne 0) { throw 'Preference persistence tests failed.' }
}
if ($SingleExe) {
    $generatedDirectory = Join-Path $PSScriptRoot 'artifacts\single-file-build'
    New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
    $manifestPath = Join-Path $generatedDirectory 'bundled-media.json'
    $manifest = @($videoNames | ForEach-Object {
        $video = Join-Path $sourcePath $_
        [pscustomobject]@{ File="Media/$_"; Bytes=(Get-Item -LiteralPath $video).Length; SHA256=(Get-FileHash -LiteralPath $video -Algorithm SHA256).Hash }
    })
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    & $sdk publish 'src\ArcadiaWeather\ArcadiaWeather.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:BundleAllAssets=true "-p:BundleMediaSource=$sourcePath" "-p:BundleManifest=$manifestPath" -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Single EXE publishing failed.' }
    $publishedFiles = @(Get-ChildItem -LiteralPath $outputPath -Recurse -File)
    if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].FullName -ne $outputExe) {
        throw 'The single EXE output unexpectedly contains loose files. Use a fresh output directory.'
    }
    $outputHash = (Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $generatedDirectory 'SHA256.txt') -Value "$outputHash  ArcadiaWeather.exe"
    Write-Host "One-file app ready: $outputExe"
    return
}
& $sdk publish 'src\ArcadiaWeather\ArcadiaWeather.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
foreach ($relative in @('libvlc\win-x64\libvlc.dll', 'libvlc\win-x64\libvlccore.dll', 'libvlc\win-x64\plugins\codec\libavcodec_plugin.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $relative))) { throw "Missing bundled player dependency: $relative" }
}
New-Item -ItemType Directory -Path $mediaOutput -Force | Out-Null
$manifest = @()
foreach ($name in $videoNames) {
    $sourceVideo = Join-Path $sourcePath $name
    $destinationVideo = Join-Path $mediaOutput $name
    if (-not [string]::Equals($sourceVideo, $destinationVideo, [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $sourceVideo -Destination $destinationVideo -Force
    }
    $sourceHash = (Get-FileHash -LiteralPath $sourceVideo -Algorithm SHA256).Hash
    $copiedHash = (Get-FileHash -LiteralPath $destinationVideo -Algorithm SHA256).Hash
    if ($sourceHash -ne $copiedHash) { throw "Video copy verification failed: $name" }
    $manifest += [pscustomobject]@{File="Media/$name"; Bytes=(Get-Item -LiteralPath $destinationVideo).Length; SHA256=$copiedHash}
    Write-Host "Bundled and verified: $name"
}
Copy-Item -LiteralPath 'README.md','THIRD-PARTY-NOTICES.txt' -Destination $outputPath -Force
$licenseOutput = Join-Path $outputPath 'Licenses'
New-Item -ItemType Directory -Path $licenseOutput -Force | Out-Null
Get-ChildItem -LiteralPath 'Licenses' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $licenseOutput -Recurse -Force }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'bundled-media.json') -Encoding utf8
$hashLines = @("$((Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash)  ArcadiaWeather.exe")
$hashLines += $manifest | ForEach-Object { "$($_.SHA256)  $($_.File)" }
$hashLines | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256.txt') -Encoding utf8
Write-Host "Ready: $outputExe"
if ($CreateArchive) {
    $archivePath = "$outputPath-portable.zip"
    $temporaryArchive = "$archivePath.$([Guid]::NewGuid().ToString('N')).partial"
    [IO.Compression.ZipFile]::CreateFromDirectory($outputPath, $temporaryArchive, [IO.Compression.CompressionLevel]::Fastest, $true)
    $zip = [IO.Compression.ZipFile]::OpenRead($temporaryArchive)
    try {
        $baseName = Split-Path -Leaf $outputPath
        foreach ($item in $manifest) {
            $entry = $zip.GetEntry("$baseName/$($item.File)")
            if ($null -eq $entry -or $entry.Length -ne $item.Bytes) { throw "Archive is missing bundled video: $($item.File)" }
            $stream = $entry.Open()
            try { $archivedHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
            finally { $stream.Dispose() }
            if ($archivedHash -ne $item.SHA256) { throw "Archive verification failed: $($item.File)" }
        }
        if ($null -eq $zip.GetEntry("$baseName/ArcadiaWeather.exe")) { throw 'Archive is missing the executable.' }
    }
    finally { $zip.Dispose() }
    [IO.File]::Move($temporaryArchive, $archivePath, $true)
    Write-Host "Portable archive verified: $archivePath"
}
