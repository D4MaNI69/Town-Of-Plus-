# Genera los PNG del rol Saboteador en Resources (Town of plus+)
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$icons = Join-Path $root "Resources\Icons"
$buttons = Join-Path $root "Resources\Buttons"
New-Item -ItemType Directory -Force -Path $icons, $buttons | Out-Null

function New-Png([string]$path, [int]$w, [int]$h, [scriptblock]$draw) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    & $draw $g $w $h
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Creado: $path"
}

$purple = [System.Drawing.Color]::FromArgb(255, 180, 60, 200)
$dark   = [System.Drawing.Color]::FromArgb(255, 30, 30, 34)
$yellow = [System.Drawing.Color]::FromArgb(255, 255, 220, 90)

# ── Icono del rol: bombilla morada apagada con grieta ────────────────────────
New-Png (Join-Path $icons "Saboteador.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $bulb = New-Object System.Drawing.SolidBrush($purple)
    $g.FillEllipse($bulb, 32, 16, 64, 64)
    $base = New-Object System.Drawing.SolidBrush($dark)
    $g.FillRectangle($base, 48, 80, 32, 18)
    $g.FillRectangle($base, 52, 102, 24, 10)
    $rayo = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 5)
    $g.DrawLines($rayo, @(
        (New-Object System.Drawing.Point(64, 26)),
        (New-Object System.Drawing.Point(54, 48)),
        (New-Object System.Drawing.Point(68, 48)),
        (New-Object System.Drawing.Point(58, 70))))
    $rayo.Dispose(); $bulb.Dispose(); $base.Dispose()
}

# ── Botón Luces Fuera: bombilla con rayos cruzados ───────────────────────────
New-Png (Join-Path $buttons "LucesFuera.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $bulb = New-Object System.Drawing.SolidBrush($purple)
    $g.FillEllipse($bulb, 34, 24, 60, 60)
    $base = New-Object System.Drawing.SolidBrush($dark)
    $g.FillRectangle($base, 48, 84, 32, 16)
    $rayo = New-Object System.Drawing.Pen($yellow, 7)
    $g.DrawLine($rayo, 20, 20, 108, 108)
    $g.DrawLine($rayo, 108, 20, 20, 108)
    $rayo.Dispose(); $bulb.Dispose(); $base.Dispose()
}

Write-Host "Assets del Saboteador generados."
