param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
if(Get-Process -Name 'Scam With Your Friends' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
$managed=Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'Scam With Your Friends_Data\Managed'
$hashes=Get-Content (Join-Path $PSScriptRoot 'original-hashes.json') -Raw | ConvertFrom-Json
foreach($name in @('ScriptsAssDef.dll','Assembly-CSharp.dll')){if((Get-FileHash -LiteralPath (Join-Path "$managed\FriendsAI.original" $name)).Hash -ne $hashes.$name){throw "Invalid backup: $name"}}
foreach($name in @('ScriptsAssDef.dll','Assembly-CSharp.dll')){Copy-Item -LiteralPath (Join-Path "$managed\FriendsAI.original" $name) -Destination (Join-Path $managed $name) -Force}
Write-Host 'Original game assemblies restored. Saved credentials and backup retained locally.'
