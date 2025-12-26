# Build-Installer.ps1
# Creates a distributable installer package for Notes app
# Run from the repository root: .\Build-Installer.ps1

param(
    [string]$OutputDir = ".\installer"
)

$AppName = "Notes"
$SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = "$SourceDir\Notes\Notes.csproj"

Write-Host "Building Notes Installer Package..." -ForegroundColor Cyan
Write-Host ""

# Check project exists
if (-not (Test-Path $ProjectPath)) {
    Write-Host "Error: Could not find Notes.csproj" -ForegroundColor Red
    exit 1
}

# Clean and create output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputDir\app" -Force | Out-Null

# Publish the application
Write-Host "1. Publishing application (self-contained)..." -ForegroundColor Yellow
dotnet publish $ProjectPath -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -o "$OutputDir\app" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed." -ForegroundColor Red
    exit 1
}
Write-Host "   Done!" -ForegroundColor Green

# Create the installer script that will be included in the package
Write-Host "2. Creating installer script..." -ForegroundColor Yellow
$installerScript = @'
# Notes Installer
# Run as: powershell -ExecutionPolicy Bypass -File Install.ps1

param(
    [switch]$Uninstall
)

$AppName = "Notes"
$InstallDir = "$env:LOCALAPPDATA\Programs\Notes"
$ExePath = "$InstallDir\Notes.exe"
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$DesktopDir = [Environment]::GetFolderPath("Desktop")
$RegPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Create-Shortcut {
    param([string]$ShortcutPath, [string]$TargetPath, [string]$Description = "")
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $TargetPath
    $Shortcut.Description = $Description
    $Shortcut.WorkingDirectory = Split-Path $TargetPath
    $Shortcut.Save()
}

if ($Uninstall) {
    Write-Host "Uninstalling $AppName..." -ForegroundColor Yellow
    Get-Process -Name "Notes" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # Remove shortcuts and registry
    Remove-Item "$StartMenuDir\$AppName.lnk" -Force -ErrorAction SilentlyContinue
    Remove-Item "$DesktopDir\$AppName.lnk" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $RegPath -Name $AppName -Force -ErrorAction SilentlyContinue

    # Remove installation
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
    }

    Write-Host "$AppName has been uninstalled." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "       Notes App Installer" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Stop running instance
Get-Process -Name "Notes" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Create installation directory
Write-Host "Installing to $InstallDir..." -ForegroundColor Yellow
if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Copy files
Copy-Item "$ScriptDir\app\*" $InstallDir -Recurse -Force
Write-Host "   Files copied" -ForegroundColor Green

# Create Start Menu shortcut
Create-Shortcut -ShortcutPath "$StartMenuDir\$AppName.lnk" -TargetPath $ExePath -Description "Notes - Quick note-taking app"
Write-Host "   Start Menu shortcut created" -ForegroundColor Green

# Create Desktop shortcut
Create-Shortcut -ShortcutPath "$DesktopDir\$AppName.lnk" -TargetPath $ExePath -Description "Notes - Quick note-taking app"
Write-Host "   Desktop shortcut created" -ForegroundColor Green

# Add to auto-start
Set-ItemProperty -Path $RegPath -Name $AppName -Value "`"$ExePath`""
Write-Host "   Auto-start enabled" -ForegroundColor Green

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "   Installation Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "To uninstall: .\Install.ps1 -Uninstall"
Write-Host ""

$StartNow = Read-Host "Start Notes now? (Y/n)"
if ($StartNow -ne "n" -and $StartNow -ne "N") {
    Start-Process $ExePath
}
'@

$installerScript | Out-File -FilePath "$OutputDir\Install.ps1" -Encoding UTF8
Write-Host "   Done!" -ForegroundColor Green

# Create a simple batch file launcher for the installer
Write-Host "3. Creating easy launcher..." -ForegroundColor Yellow
@"
@echo off
echo Notes App Installer
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
pause
"@ | Out-File -FilePath "$OutputDir\Install-Notes.bat" -Encoding ASCII
Write-Host "   Done!" -ForegroundColor Green

# Create README
Write-Host "4. Creating README..." -ForegroundColor Yellow
@"
# Notes App Installer

## Installation
Double-click `Install-Notes.bat` or run:
    powershell -ExecutionPolicy Bypass -File Install.ps1

## What Gets Installed
- Application: %LOCALAPPDATA%\Programs\Notes
- Start Menu shortcut
- Desktop shortcut
- Auto-start on login

## Uninstall
Run: powershell -ExecutionPolicy Bypass -File Install.ps1 -Uninstall

## Features
- Quick note-taking with rich text
- Clipboard history monitoring
- System tray integration
- Toast notifications
- Auto-save
- Light/Dark theme support
"@ | Out-File -FilePath "$OutputDir\README.txt" -Encoding UTF8
Write-Host "   Done!" -ForegroundColor Green

# Summary
$size = [math]::Round((Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   Installer Package Created!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Location: $((Resolve-Path $OutputDir).Path)"
Write-Host "   Size: ${size} MB"
Write-Host ""
Write-Host "   To distribute:"
Write-Host "   1. Copy the '$OutputDir' folder to other machines"
Write-Host "   2. Run Install-Notes.bat"
Write-Host ""
Write-Host "   Or create a ZIP:" -ForegroundColor Yellow
Write-Host "   Compress-Archive -Path '$OutputDir\*' -DestinationPath 'Notes-Installer.zip'"
Write-Host ""
