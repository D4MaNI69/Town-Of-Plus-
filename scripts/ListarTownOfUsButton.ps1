$b=[IO.File]::ReadAllBytes('c:\Users\Admin\Pictures\Nueva carpeta\Town of plus+\packages.local\miraapi_extract\lib\net6.0\MiraAPI.dll')
$t=[Text.Encoding]::ASCII.GetString($b)
foreach($p in @('OnEffectEnd','EffectDuration','EffectActive','ActionBehaviour','Vibrate')){
  if($t.Contains($p)){Write-Host ('SI: '+$p)}else{Write-Host ('no: '+$p)}
}