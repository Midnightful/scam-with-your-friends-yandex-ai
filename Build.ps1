param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$managed=Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'Scam With Your Friends_Data\Managed'
$build=Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$core='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll'
if(!(Test-Path -LiteralPath $core)){throw 'Install the .NET Framework 4.8 Developer Pack first.'}
$refs=(Get-ChildItem -LiteralPath $managed -Filter *.dll | Where-Object Name -ne 'FriendsAI.dll' | ForEach-Object { $path=if($_.Name -eq 'mscorlib.dll'){$core}else{$_.FullName}; '<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($path)+'</HintPath><Private>false</Private></Reference>' }) -join "`n"
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>FriendsAI</AssemblyName><NoStdLib>true</NoStdLib><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences><GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute><EnableDefaultCompileItems>false</EnableDefaultCompileItems><LangVersion>latest</LangVersion></PropertyGroup><ItemGroup><Compile Include="..\src\*.cs" />'+$refs+'</ItemGroup></Project>'
Set-Content -LiteralPath (Join-Path $build 'FriendsAI.csproj') -Value $project
& dotnet build (Join-Path $build 'FriendsAI.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Add-on build failed'}
$original=Join-Path $managed 'FriendsAI.original'
if(!(Test-Path -LiteralPath $original)){$original=$managed}
& dotnet run --project (Join-Path $root 'tools\Patcher.csproj') -c Release -- $original (Join-Path $build 'patch') (Join-Path $build 'bin\Release\net10.0\FriendsAI.dll')
if($LASTEXITCODE -ne 0){throw 'Patcher failed'}
Write-Host 'Prepared local patch in build/patch. No credentials were read or exported.'
