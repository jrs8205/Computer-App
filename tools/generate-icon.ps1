# generate-icon.ps1 — generoi src/HardwareMonitor.App/Assets/app.ico samasta
# designista kuin Assets/icon.svg (tumma pyöristetty neliö, syaani mittarikaari
# neuloineen, vihreä sykeviiva). Ajo repon juuressa:  .\tools\generate-icon.ps1
Add-Type -AssemblyName System.Drawing

function Draw-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $s = $size / 256.0

    # Tausta: pyöristetty neliö pystygradientilla
    $rect = New-Object System.Drawing.Rectangle ([int](8*$s)), ([int](8*$s)), ([int](240*$s)), ([int](240*$s))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.ColorTranslator]::FromHtml('#2A2A2E')),
        ([System.Drawing.ColorTranslator]::FromHtml('#161618')),
        ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $r = [Math]::Max(2.0, 52 * $s)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc([float]$rect.X, [float]$rect.Y, [float](2*$r), [float](2*$r), 180, 90)
    $path.AddArc([float]($rect.Right - 2*$r), [float]$rect.Y, [float](2*$r), [float](2*$r), 270, 90)
    $path.AddArc([float]($rect.Right - 2*$r), [float]($rect.Bottom - 2*$r), [float](2*$r), [float](2*$r), 0, 90)
    $path.AddArc([float]$rect.X, [float]($rect.Bottom - 2*$r), [float](2*$r), [float](2*$r), 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)

    # Mittarikaari (180 astetta ylös)
    $penArc = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#4FC3F7')), ([float](18*$s))
    $penArc.StartCap = 'Round'; $penArc.EndCap = 'Round'
    $g.DrawArc($penArc, [float](50*$s), [float](72*$s), [float](156*$s), [float](156*$s), 180, 180)

    # Neula + napa
    $penNeedle = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#E8F5E9')), ([float](11*$s))
    $penNeedle.StartCap = 'Round'; $penNeedle.EndCap = 'Round'
    $g.DrawLine($penNeedle, [float](128*$s), [float](150*$s), [float](180*$s), [float](98*$s))
    $hub = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#4FC3F7'))
    $g.FillEllipse($hub, [float]((128-13)*$s), [float]((150-13)*$s), [float](26*$s), [float](26*$s))

    # Sykeviiva
    $penPulse = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#A5D6A7')), ([float](11*$s))
    $penPulse.StartCap = 'Round'; $penPulse.EndCap = 'Round'; $penPulse.LineJoin = 'Round'
    $pts = @(48,206, 88,206, 104,184, 122,224, 138,206, 208,206)
    $points = [System.Drawing.PointF[]]::new(6)
    for ($i = 0; $i -lt $pts.Count; $i += 2) {
        $points[$i / 2] = New-Object System.Drawing.PointF ([float]($pts[$i]*$s)), ([float]($pts[$i+1]*$s))
    }
    $g.DrawLines($penPulse, $points)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) { ,(Draw-IconPng $size) }

# ICO-kontti: ICONDIR (6 t) + ICONDIRENTRY (16 t/kuva) + PNG-datat
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }

$assetsDir = Join-Path $PSScriptRoot "..\src\HardwareMonitor.App\Assets"
New-Item -ItemType Directory -Force $assetsDir | Out-Null
$icoPath = Join-Path $assetsDir "app.ico"
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
Write-Host "app.ico generoitu ($($out.Length) tavua): $icoPath"
