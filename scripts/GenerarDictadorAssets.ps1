# Genera los PNG del rol Dictador (Town of plus+)
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

$imp   = [System.Drawing.Color]::FromArgb(255, 255, 0, 0)   # rojo impostor
$grista = [System.Drawing.Color]::FromArgb(255, 230, 230, 235)
$black = [System.Drawing.Color]::FromArgb(255, 20, 20, 24)
$gold  = [System.Drawing.Color]::FromArgb(255, 255, 200, 40)

# Icono del rol: corona roja impositora / título
New-Png (Join-Path $icons "Dictador.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $crown = New-Object System.Drawing.SolidBrush($gold)
    $p = New-Object System.Drawing.Pen($imp, 6)
    # corona
    $pts = @(
        (New-Object System.Drawing.PointF(28, 66)),
        (New-Object System.Drawing.PointF(42, 40)),
        (New-Object System.Drawing.PointF(60, 58)),
        (New-Object System.Drawing.PointF(80, 36)),
        (New-Object System.Drawing.PointF(100, 60)),
        (New-Object System.Drawing.PointF(112, 32)),
        (New-Object System.Drawing.PointF(120, 70))
    )
    $g.DrawPolygon($p, $pts)
    $g.FillPolygon($crown, $pts)
    $g.FillRectangle([System.Drawing.SolidBrush]($black), 28, 76, 92, 24)
    $g.DrawEllipse($p, 34, 82, 12, 12)
    $g.DrawEllipse($p, 82, 82, 12, 12)
    $crown.Dispose(); $p.Dispose()
}

# Botón Reveal: gaveta de la mayoría / "reveal"
New-Png (Join-Path $buttons "DictadorReveal.png") 256 256 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $b = New-Object System.Drawing.SolidBrush($imp)
    $g.FillEllipse($b, 48, 32, 160, 160)      # círculo rojo (oratoria)
    $mic = New-Object System.Drawing.SolidBrush($black)
    $g.FillRectangle($mic, 112, 100, 32, 10)  # mango micrófono
    $g.FillEllipse($mic, 100, 52, 56, 48)     # cabeza micrófono
    $g.FillRectangle($mic, 108, 136, 40, 56)  # base
    $ring = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 230, 230, 235), 8)
    $g.DrawEllipse($ring, 48, 32, 160, 160)
    $b.Dispose(); $mic.Dispose(); $ring.Dispose()
}

Write-Host "Assets del Dictador generados."