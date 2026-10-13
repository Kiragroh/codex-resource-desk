# Mechanical PNG-to-multi-resolution-ICO export. The GPT Image master is unchanged.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
$assets = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/ResourceDesk.App/Assets'
$source = [Windows.Media.Imaging.BitmapImage]::new([Uri](Join-Path $assets 'logo.png'))
$sizes = @(16,24,32,48,64,128,256)
$frames = foreach ($size in $sizes) {
    $scaled = [Windows.Media.Imaging.TransformedBitmap]::new($source, [Windows.Media.ScaleTransform]::new($size / $source.PixelWidth, $size / $source.PixelHeight))
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($scaled))
    $memory = [IO.MemoryStream]::new()
    $encoder.Save($memory)
    ,$memory.ToArray()
    $memory.Dispose()
}
$file = [IO.File]::Create((Join-Path $assets 'ResourceDesk.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $edge = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$edge); $writer.Write([byte]$edge)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'Exported 7 icon sizes from the unchanged GPT Image logo.'
