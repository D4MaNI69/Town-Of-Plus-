# Genera los PNG del rol Kamikaze en Resources (Town of plus+)
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$icons = Join-Path $root "Resources\Icons"
$buttons = Join-Path $root "Resources\Buttons"
$kamikaze = Join-Path $root "Resources\Kamikaze"
New-Item -ItemType Directory -Force -Path $icons, $buttons, $kamikaze | Out-Null

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

$orange = [System.Drawing.Color]::FromArgb(255, 255, 94, 0)
$dark   = [System.Drawing.Color]::FromArgb(255, 30, 30, 34)
$gray   = [System.Drawing.Color]::FromArgb(255, 130, 134, 142)
$lgray  = [System.Drawing.Color]::FromArgb(255, 190, 194, 202)
$skin   = [System.Drawing.Color]::FromArgb(255, 90, 160, 255)

# ── Icono del rol: bomba naranja con chispa ──────────────────────────────────
New-Png (Join-Path $icons "Kamikaze.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b = New-Object System.Drawing.SolidBrush($orange)
    $fuse = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 60, 40, 20), 5)
    $g.FillEllipse($b, 30, 48, 68, 68)
    $g.DrawRectangle($fuse, 58, 36, 12, 12)
    $g.DrawBezier($fuse, 64, 36, 70, 22, 84, 24, 92, 12)
    $spark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Yellow)
    $g.FillEllipse($spark, 86, 4, 16, 16)
    $h1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 255, 255, 255))
    $g.FillEllipse($h1, 40, 58, 16, 12)
    $b.Dispose(); $fuse.Dispose(); $spark.Dispose(); $h1.Dispose()
}

# ── Botón Engaño: bomba oscura con signo de interrogación ────────────────────
New-Png (Join-Path $buttons "Engano.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b = New-Object System.Drawing.SolidBrush($dark)
    $ring = New-Object System.Drawing.Pen($orange, 10)
    $g.FillEllipse($b, 48, 88, 160, 160)
    $g.DrawEllipse($ring, 48, 88, 160, 160)
    $g.FillEllipse($b, 108, 56, 40, 40)
    $f = New-Object System.Drawing.Font("Arial", 72, [System.Drawing.FontStyle]::Bold)
    $t = New-Object System.Drawing.SolidBrush($orange)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = "Center"; $fmt.LineAlignment = "Center"
    $g.DrawString("?", $f, $t, (New-Object System.Drawing.RectangleF(48, 100, 160, 140)), $fmt)
    $b.Dispose(); $ring.Dispose(); $f.Dispose(); $t.Dispose(); $fmt.Dispose()
}

# ── Engaño 1: Cambiaformas (dos siluetas superpuestas) ──────────────────────
New-Png (Join-Path $buttons "Engano1.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(120, 255, 94, 0))
    $b2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 130, 134, 142))
    $g.FillEllipse($b1, 30, 40, 196, 196)
    $g.FillRectangle($b2, 96, 96, 64, 110)
    $g.FillEllipse($b2, 88, 76, 80, 44)
    $v = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 140, 210, 255))
    $g.FillRectangle($v, 104, 120, 24, 24)
    $b1.Dispose(); $b2.Dispose(); $v.Dispose()
}

# ── Engaño 2: Camuflaje (ojo tachado) ───────────────────────────────────────
New-Png (Join-Path $buttons "Engano2.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $p = New-Object System.Drawing.Pen($lgray, 14)
    $b = New-Object System.Drawing.SolidBrush($orange)
    $g.DrawBezier($p, 32, 128, 88, 48, 168, 48, 224, 128)
    $g.DrawBezier($p, 32, 128, 88, 208, 168, 208, 224, 128)
    $g.FillEllipse($b, 104, 104, 48, 48)
    $slash = New-Object System.Drawing.Pen($dark, 16)
    $g.DrawLine($slash, 40, 216, 216, 40)
    $p.Dispose(); $b.Dispose(); $slash.Dispose()
}

# ── Engaño 3: Ducto Falso (rejilla de ventilación) ──────────────────────────
New-Png (Join-Path $buttons "Engano3.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b = New-Object System.Drawing.SolidBrush($gray)
    $g.FillRectangle($b, 40, 76, 176, 104)
    $dark2 = New-Object System.Drawing.SolidBrush($dark)
    foreach ($i in 0..2) { $g.FillRectangle($dark2, 56, (92 + $i * 28), 144, 16) }
    $p = New-Object System.Drawing.Pen($orange, 8)
    $g.DrawRectangle($p, 40, 76, 176, 104)
    $b.Dispose(); $dark2.Dispose(); $p.Dispose()
}

# ── Engaño 4: Arrastre (cuerpo con mano) ────────────────────────────────────
New-Png (Join-Path $buttons "Engano4.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 160, 60, 60))
    $g.FillRectangle($b, 88, 96, 120, 76)
    $g.FillEllipse($b, 56, 116, 44, 40)
    $hand = New-Object System.Drawing.SolidBrush($skin)
    $g.FillEllipse($hand, 196, 60, 40, 40)
    $arrow = New-Object System.Drawing.Pen($orange, 10)
    $g.DrawBezier($arrow, 210, 100, 200, 130, 190, 140, 180, 150)
    $b.Dispose(); $hand.Dispose(); $arrow.Dispose()
}

# ── Sprite del ducto falso en el mapa ───────────────────────────────────────
New-Png (Join-Path $kamikaze "DuctoFalso.png") 200 140 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $frame = New-Object System.Drawing.SolidBrush($gray)
    $inner = New-Object System.Drawing.SolidBrush($dark)
    $g.FillRectangle($frame, 10, 20, 180, 100)
    $g.FillRectangle($inner, 24, 34, 152, 72)
    foreach ($i in 0..3) {
        $g.FillRectangle($frame, 24, (38 + $i * 18), 152, 8)
    }
    $p = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 80, 84, 90), 6)
    $g.DrawRectangle($p, 10, 20, 180, 100)
    $frame.Dispose(); $inner.Dispose(); $p.Dispose()
}

Write-Host "Assets del Kamikaze generados."
