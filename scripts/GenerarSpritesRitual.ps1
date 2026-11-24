# Genera los PNG placeholder del Ritualista (color crema) en Resources.
# El usuario puede reemplazarlos por sus propios .png con los mismos nombres.
Add-Type -AssemblyName System.Drawing

$crema = [System.Drawing.Color]::FromArgb(255, 255, 253, 208)
$outDir = Join-Path $PSScriptRoot "..\Resources"

function New-Pen([float]$width) {
    $pen = New-Object System.Drawing.Pen($crema, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function New-Lienzo([int]$size) {
    $script:bmp = New-Object System.Drawing.Bitmap($size, $size)
    $script:g = [System.Drawing.Graphics]::FromImage($script:bmp)
    $script:g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $script:g.Clear([System.Drawing.Color]::Transparent)
}

function Add-Estrella($g, $pen, [float]$cx, [float]$cy, [float]$rOut, [float]$rIn, [int]$puntas) {
    $pts = New-Object 'System.Drawing.PointF[]' ($puntas * 2)
    for ($i = 0; $i -lt $puntas * 2; $i++) {
        $ang = [Math]::PI / 2 + $i * [Math]::PI / $puntas
        $r = if ($i % 2 -eq 0) { $rOut } else { $rIn }
        $pts[$i] = New-Object System.Drawing.PointF(
            [float]($cx + [Math]::Cos($ang) * $r), [float]($cy + [Math]::Sin($ang) * $r))
    }
    $g.DrawPolygon($pen, $pts)
}

function Save-Png([string]$name) {
    $path = Join-Path $outDir $name
    $script:bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose(); $script:bmp.Dispose()
    Write-Host "Generado: $path"
}

# 1) Icono del rol: circulo + estrella + punto central (256x256, ppu 200)
New-Lienzo 256
$pen = New-Pen 11
$g.DrawEllipse($pen, 128 - 118, 128 - 118, 236, 236)
Add-Estrella $g $pen 128 128 82 35 5
$brush = New-Object System.Drawing.SolidBrush($crema)
$g.FillEllipse($brush, 128 - 20, 128 - 20, 40, 40)
Save-Png "ritualista.png"

# 2) Boton Ritual: circulo + estrella (256x256, ppu 200)
New-Lienzo 256
$pen = New-Pen 14
$g.DrawEllipse($pen, 128 - 112, 128 - 112, 224, 224)
Add-Estrella $g $pen 128 128 72 30 5
Save-Png "RitualBoton.png"

# 3) Boton Sacrificar: daga (256x256, ppu 200)
New-Lienzo 256
$pen = New-Pen 13
$g.DrawPolygon($pen, @(
    (New-Object System.Drawing.PointF(128, 26)),
    (New-Object System.Drawing.PointF(85, 154)),
    (New-Object System.Drawing.PointF(171, 154))))
$g.DrawLine($pen, 128, 154, 128, 230)
$g.DrawLine($pen, 92, 194, 164, 194)
Save-Png "SacrificarBoton.png"

Write-Host "Listo."