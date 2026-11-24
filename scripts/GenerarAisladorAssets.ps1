# Genera los PNG placeholder del rol Aislador (Town of plus+), color #109FB9.
# El usuario puede reemplazarlos por sus propios .png con los mismos nombres.
Add-Type -AssemblyName System.Drawing

$root    = Split-Path -Parent $PSScriptRoot
$color   = [System.Drawing.Color]::FromArgb(255, 16, 159, 185)
$icons   = Join-Path $root "Resources\Icons"
$buttons = Join-Path $root "Resources\Buttons"
$banners = Join-Path $root "Resources\Banner"
New-Item -ItemType Directory -Force -Path $icons, $buttons, $banners | Out-Null

function New-Png($path, $w, $h, $draw) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    & $draw $g $w $h
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "OK: $path"
}

function Dibuja-Burbuja($g, $cx, $cy, $r) {
    $pen = New-Object System.Drawing.Pen($color, 4)
    $g.DrawEllipse($pen, $cx - $r, $cy - $r, $r * 2, $r * 2)
    $pen.Dispose()
}

# ── Icono del rol: persona dentro de una burbuja ────────────────────────────
New-Png (Join-Path $icons "Aislador.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    Dibuja-Burbuja $g 64 60 46
    $pen = New-Object System.Drawing.Pen($color, 5)
    $g.DrawEllipse($pen, 52, 28, 24, 24)          # cabeza
    $g.DrawRectangle($pen, 46, 58, 36, 30)        # cuerpo
    $pen.Dispose()
}

# ── Botón Soplar ────────────────────────────────────────────────────────────
New-Png (Join-Path $buttons "Soplar.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    Dibuja-Burbuja $g 84 64 20
    Dibuja-Burbuja $g 56 40 10
    Dibuja-Burbuja $g 56 90 10
    Dibuja-Burbuja $g 84 106 7
    $pen = New-Object System.Drawing.Pen($color, 4)
    $g.DrawArc($pen, 16, 40, 32, 48, 110, 140)    # boca que sopla
    $pen.Dispose()
}

# ── Botón Burbuja (PNG compartido: marcados y escudo) ───────────────────────
New-Png (Join-Path $buttons "Burbuja.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    Dibuja-Burbuja $g 64 64 44
    $pen = New-Object System.Drawing.Pen($color, 3)
    $g.DrawArc($pen, 60, 36, 20, 20, 130, 90)     # brillo
    $pen.Dispose()
}

# ── PNG suelto de burbuja (marcados + escudo): Resources/BurbujaEfecto.png ──
New-Png (Join-Path $root "Resources\BurbujaEfecto.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    Dibuja-Burbuja $g 64 64 48
    $pen = New-Object System.Drawing.Pen($color, 3)
    $g.DrawArc($pen, 58, 32, 22, 22, 130, 90)     # brillo
    $pen.Dispose()
}

# ── Botón Escudo ────────────────────────────────────────────────────────────
New-Png (Join-Path $buttons "Escudo.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    Dibuja-Burbuja $g 64 64 44
    $pen = New-Object System.Drawing.Pen($color, 5)
    $g.DrawLines($pen, @(
        (New-Object System.Drawing.Point(44, 46)),
        (New-Object System.Drawing.Point(84, 46)),
        (New-Object System.Drawing.Point(84, 68)),
        (New-Object System.Drawing.Point(64, 90)),
        (New-Object System.Drawing.Point(44, 68)),
        (New-Object System.Drawing.Point(44, 46))))
    $pen.Dispose()
}

# ── Banner placeholder (mismo formato que los demás: Resources/Banner/) ─────
New-Png (Join-Path $banners "aislador.png") 372 232 {
    param($g, $w, $h)
    $g.Clear($color)
    $oscuro = [System.Drawing.Color]::FromArgb(255, 9, 88, 102)
    $brush = New-Object System.Drawing.SolidBrush($oscuro)
    $g.FillRectangle($brush, 0, 0, 372, 34)
    $brush.Dispose()
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $font = New-Object System.Drawing.Font('Arial', 20, [System.Drawing.FontStyle]::Bold)
    $g.DrawString("AISLADOR", $font, $brush, 14, 2)
    $font2 = New-Object System.Drawing.Font('Arial', 11)
    $g.DrawString("Crewmate Support - placeholder", $font2, $brush, 14, 96)
    $brush.Dispose(); $font.Dispose(); $font2.Dispose()
}

Write-Host "Assets del Aislador generados."

