param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
if(Get-Process -Name 'Scam With Your Friends' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
$managed=Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'Scam With Your Friends_Data\Managed'
$backup=Join-Path $managed 'FriendsAI.original'
$hashes=Get-Content (Join-Path $PSScriptRoot 'original-hashes.json') -Raw | ConvertFrom-Json
foreach($name in @('ScriptsAssDef.dll','Assembly-CSharp.dll')){
 $source=if(Test-Path -LiteralPath (Join-Path $backup $name)){Join-Path $backup $name}else{Join-Path $managed $name}
 if((Get-FileHash -LiteralPath $source).Hash -ne $hashes.$name){throw "Unsupported game build or invalid original backup: $name"}
}
& (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $GamePath
New-Item -ItemType Directory -Force -Path $backup | Out-Null
foreach($name in @('ScriptsAssDef.dll','Assembly-CSharp.dll')){if(!(Test-Path -LiteralPath (Join-Path $backup $name))){Copy-Item -LiteralPath (Join-Path $managed $name) -Destination (Join-Path $backup $name)}}
foreach($name in @('ScriptsAssDef.dll','Assembly-CSharp.dll','FriendsAI.dll')){
 $patch=Join-Path $PSScriptRoot "build\patch\$name"
 Copy-Item -LiteralPath $patch -Destination (Join-Path $managed $name) -Force
 if((Get-FileHash -LiteralPath $patch).Hash -ne (Get-FileHash -LiteralPath (Join-Path $managed $name)).Hash){throw "Install verification failed: $name"}
}
Write-Host 'Installed. Restart the game. Saved credentials were not exported.'
