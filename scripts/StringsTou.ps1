$ruta = 'C:\Users\Admin\.nuget\packages\townofusmira\1.7.2\lib\net6.0\TownOfUsMira.dll'
$bytes = [IO.File]::ReadAllBytes($ruta)
$texto = [Text.Encoding]::ASCII.GetString($bytes)
$matches = [regex]::Matches($texto, '[\x20-\x7E]{4,}')
$patrones = 'Flash','Fade','Overlay','Blink','AttemptMurder','ShieldBroken','SpriteCreateScreen','FullScre'
$vistas = @{}
foreach ($m in $matches) {
  $s = $m.Value
  if ($s.Length -gt 200) { continue }
  foreach ($p in $patrones) {
    if ($s -match $p) { if (-not $vistas.ContainsKey($s)) { $vistas[$s]=1; Write-Output $s } break }
  }
}