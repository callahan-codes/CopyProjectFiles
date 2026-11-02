<#
  Publishes CopyProjectFiles as a self-contained Windows app (no .NET install needed to run it)
  and adds a Start menu shortcut you can pin to the taskbar.

  Run from the v3 folder (next to CopyProjectFiles.sln):
      powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1

  Run it again any time to update the installed copy. Your saved projects live in
  %APPDATA%\CopyProjectFiles, so republishing never touches them.
#>
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\CopyProjectFiles",
    [string]$Runtime = "win-x64"   # use win-arm64 on an ARM PC
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\CopyProjectFiles.Desktop\CopyProjectFiles.Desktop.csproj'

if (-not (Test-Path $project)) {
    throw "Can't find $project. Run this script from the folder that contains CopyProjectFiles.sln."
}

if (Get-Process -Name CopyProjectFiles -ErrorAction SilentlyContinue) {
    throw "CopyProjectFiles is running. Close it first so its files can be replaced."
}

Write-Host "Publishing to $InstallDir ..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

$exe = Join-Path $InstallDir 'CopyProjectFiles.exe'

# Start menu shortcut (Windows doesn't allow scripts to pin to the taskbar; you pin it once by hand)
$startMenu = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $startMenu 'CopyProjectFiles.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = 'Copy project files into a clean folder for Claude'
$shortcut.Save()

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  App:      $exe"
Write-Host "  Shortcut: $shortcutPath"
Write-Host ""
Write-Host "To pin it: press Start, type CopyProjectFiles, right-click it, choose 'Pin to taskbar'."