$dll = 'C:\Users\Admin\.nuget\packages\amongus.gamelibs.steam\2026.8.18\build\Assembly-CSharp.dll'
$dir = Split-Path $dll
$onResolve = [System.ResolveEventHandler] {
    param($s, $e)
    $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $script:dir "$name.dll"
    if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFile($p) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($onResolve)
$asm = [System.Reflection.Assembly]::LoadFile($dll)
$types = $asm.GetExportedTypes() | Where-Object { $_.Name -like '*ShipStatus*' }
$types | ForEach-Object { $_.FullName }
$t = $types | Select-Object -First 1
'---Light methods---'
$t.GetMethods() | Where-Object { $_.Name -like '*Light*' } | ForEach-Object { $_.ToString() }
'---LogicOptions---'
$lo = $asm.GetExportedTypes() | Where-Object { $_.Name -eq 'LogicOptions' }
$lo | ForEach-Object { $_.FullName }
$lo.GetMethods() | Where-Object { $_.Name -like '*Option*' -or $_.Name -like '*Light*' } | Select-Object -First 20 | ForEach-Object { $_.ToString() }