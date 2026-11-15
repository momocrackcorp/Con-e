$ErrorActionPreference = 'Stop'
if (-not (Test-Path output/LibreHardwareMonitorLib.dll)) { throw 'Falta la biblioteca de sensores en el paquete portátil' }
New-Item output/Licenses, output/Sensors -ItemType Directory -Force | Out-Null
Copy-Item Licenses/* output/Licenses/
Copy-Item THIRD_PARTY_NOTICES.md output/
$source = 'https://raw.githubusercontent.com/LibreHardwareMonitor/LibreHardwareMonitor/v0.9.6/LibreHardwareMonitor/Resources/PawnIO_setup.exe'
Invoke-WebRequest $source -OutFile output/Sensors/PawnIO_setup.exe
$signature = Get-AuthenticodeSignature output/Sensors/PawnIO_setup.exe
if ($signature.Status -ne 'Valid') { throw "El instalador PawnIO no tiene una firma válida: $($signature.Status)" }
@("Origen: $source", "Firmante: $($signature.SignerCertificate.Subject)", "SHA256: $((Get-FileHash output/Sensors/PawnIO_setup.exe -Algorithm SHA256).Hash)", 'Sólo se instala a petición explícita desde Temperaturas y sensores.') | Set-Content output/Sensors/LEEME.txt
$assets = Get-Content src/obj/project.assets.json -Raw | ConvertFrom-Json -AsHashtable
$cache = @($assets.packageFolders.Keys)[0]
$notices = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $assets.libraries.GetEnumerator()) {
  if ($entry.Value.type -ne 'package') { continue }
  $folder = Join-Path $cache $entry.Value.path
  $nuspec = Get-ChildItem $folder -Filter '*.nuspec' | Select-Object -First 1
  if (-not $nuspec) { throw "Faltan metadatos de $($entry.Key)" }
  [xml]$spec = Get-Content $nuspec.FullName -Raw
  $meta = $spec.SelectSingleNode('//*[local-name()="metadata"]')
  $license = $meta.SelectSingleNode('*[local-name()="license"]')
  $url = $meta.SelectSingleNode('*[local-name()="licenseUrl"]')
  $project = $meta.SelectSingleNode('*[local-name()="projectUrl"]')
  $id = $entry.Key.Replace('/','-')
  $notices.Add("$($entry.Key) | $($project.InnerText) | $($license.InnerText) | $($url.InnerText)")
  if ($license -and $license.type -eq 'file') {
    Copy-Item (Join-Path $folder $license.InnerText) "output/Licenses/$id.txt"
  } elseif ($license -and $license.type -eq 'expression') {
    Invoke-WebRequest ('https://licenses.nuget.org/' + [Uri]::EscapeDataString($license.InnerText)) -OutFile "output/Licenses/$id.html"
  } elseif ($url -and $url.InnerText -notlike '*license/placeholder*') {
    Invoke-WebRequest $url.InnerText -OutFile "output/Licenses/$id.html"
  } else { throw "Revisar licencia de $($entry.Key)" }
}
$notices | Set-Content output/Licenses/packages.txt
