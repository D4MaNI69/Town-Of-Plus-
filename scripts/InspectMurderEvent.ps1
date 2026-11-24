$a=[System.Reflection.Assembly]::LoadFile('C:\Users\Admin\.nuget\packages\allofus.miraapi\0.5.0\lib\net6.0\MiraAPI.dll')
try { $tipos = $a.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $tipos = $_.Exception.Types | Where-Object { $_ -ne $null } }
$tipos | Where-Object { $_.Name -like '*Murder*' } | ForEach-Object {
  Write-Output ("== " + $_.FullName)
  $_.GetProperties() | ForEach-Object { Write-Output ("   " + $_.Name + " : " + $_.PropertyType.Name) }
  $_.GetFields() | ForEach-Object { Write-Output ("   f: " + $_.Name + " : " + $_.FieldType.Name) }
}