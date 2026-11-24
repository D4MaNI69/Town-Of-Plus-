# Genera los PNG del rework ESCAPE del Ermitaño (Town of plus+)
# Todos los recursos viven SIEMPRE dentro de Resources del proyecto.
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$buttons = Join-Path $root "Resources\Buttons"
$recursos = Join-Path $root "Resources"
New-Item -ItemType Directory -Force -Path $buttons, $recursos | Out-Null

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

# Color celeste del Ermitaño (#70C6FB).
$celeste = [System.Drawing.Color]::FromArgb(255, 112, 198, 251)

# Botón ESCAPE: caja de origen + flecha que escapa (128x128, igual que el resto
# de botones del rol; el ppu 150 del código lo deja al tamaño de los demás).
New-Png (Join-Path $buttons "Escape.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $pen = New-Object System.Drawing.Pen($celeste, 7)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Caja de origen (abierta por arriba a la derecha).
    $g.DrawLine($pen, 20, 104, 74, 104)
    $g.DrawLine($pen, 20, 104, 20, 52)
    $g.DrawLine($pen, 20, 52, 48, 52)
    $g.DrawLine($pen, 74, 104, 74, 78)

    # Flecha diagonal hacia fuera.
    $g.DrawLine($pen, 50, 50, 106, 106)
    $g.DrawLine($pen, 106, 106, 74, 106)
    $g.DrawLine($pen, 106, 106, 106, 74)

    $pen.Dispose()
}

# PNG del CURSOR de Escape: retícula de teletransporte (128x128).
# La opacidad del 70% la aplica el código en el SpriteRenderer.
New-Png (Join-Path $recursos "EscapeCursor.png") 128 128 {
    param($g, $w, $h)
    $g.Clear([System.Drawing.Color]::Transparent)
    $pen = New-Object System.Drawing.Pen($celeste, 7)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Retícula: dos círculos + cruz con punto central.
    $g.DrawEllipse($pen, 18, 18, 92, 92)
    $g.DrawEllipse($pen, 43, 43, 42, 42)
    $g.DrawLine($pen, 4, 64, 18, 64)
    $g.DrawLine($pen, 110, 64, 124, 64)
    $g.DrawLine($pen, 64, 4, 64, 18)
    $g.DrawLine($pen, 64, 110, 64, 124)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($celeste)), 58, 58, 12, 12)

    $pen.Dispose()
}

Write-Host "Assets del rework Escape del Ermitano generados (Resources)."
