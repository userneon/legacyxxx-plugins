# Builds LegacyX-Checker.msi. Needs the .NET 8 SDK; the WiX tool is fetched by the build.
#   cd LegacyX-Checker-Installer
#   .\build-msi.ps1
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "publish"
dotnet publish (Join-Path $here "..\LegacyX-Checker\LegacyX-Checker.csproj") -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
dotnet build (Join-Path $here "LegacyX-Checker-Installer.wixproj") -c Release "-p:ExePath=$out\LegacyX-Checker.exe"
Write-Host ""
Write-Host "Done: $here\bin\Release\LegacyX-Checker.msi"
Get-FileHash "$here\bin\Release\LegacyX-Checker.msi" -Algorithm SHA256 | Format-List
