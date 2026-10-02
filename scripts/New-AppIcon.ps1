param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\ArcadiaWeather\Assets\butterfly.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\ArcadiaWeather\Assets\arcadia.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# Package the supplied transparent artwork into standard Windows icon sizes.
# The source PNG stays unchanged; each frame preserves its aspect ratio.
$sourceImage = [System.Drawing.Image]::FromFile([IO.Path]::GetFullPath($Source))
$frames = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scale = [Math]::Min($size / $sourceImage.Width, $size / $sourceImage.Height)
            $width = [int][Math]::Round($sourceImage.Width * $scale)
            $height = [int][Math]::Round($sourceImage.Height * $scale)
            $rectangle = [System.Drawing.Rectangle]::new([int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height)
            $graphics.DrawImage($sourceImage, $rectangle)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{Size = $size; Data = $stream.ToArray()})
        }
        finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
}
finally { $sourceImage.Dispose() }
$output = [IO.File]::Create([IO.Path]::GetFullPath($Destination))
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
}
finally { $writer.Dispose(); $output.Dispose() }
Write-Output "Created butterfly icon with $($frames.Count) sizes."
