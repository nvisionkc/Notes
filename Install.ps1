# Notes App Installer Script
# Run as: powershell -ExecutionPolicy Bypass -File Install.ps1

param(
    [switch]$Uninstall
)

$AppName = "Notes"
$Publisher = "Local"
$InstallDir = "$env:LOCALAPPDATA\Programs\Notes"
$ExePath = "$InstallDir\Notes.exe"
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$DesktopDir = [Environment]::GetFolderPath("Desktop")
$RegPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

function Create-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$Description = "",
        [string]$IconPath = ""
    )

    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $TargetPath
    $Shortcut.Description = $Description
    $Shortcut.WorkingDirectory = Split-Path $TargetPath
    if ($IconPath) {
        $Shortcut.IconLocation = $IconPath
    }
    $Shortcut.Save()
}

if ($Uninstall) {
    Write-Host "Uninstalling $AppName..." -ForegroundColor Yellow

    # Stop running instance
    Get-Process -Name "Notes" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # Remove Start Menu shortcut
    $StartMenuShortcut = "$StartMenuDir\$AppName.lnk"
    if (Test-Path $StartMenuShortcut) {
        Remove-Item $StartMenuShortcut -Force
        Write-Host "  Removed Start Menu shortcut" -ForegroundColor Green
    }

    # Remove Desktop shortcut
    $DesktopShortcut = "$DesktopDir\$AppName.lnk"
    if (Test-Path $DesktopShortcut) {
        Remove-Item $DesktopShortcut -Force
        Write-Host "  Removed Desktop shortcut" -ForegroundColor Green
    }

    # Remove auto-start registry entry
    if (Get-ItemProperty -Path $RegPath -Name $AppName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RegPath -Name $AppName -Force
        Write-Host "  Removed auto-start entry" -ForegroundColor Green
    }

    # Remove installation directory
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "  Removed installation directory" -ForegroundColor Green
    }

    Write-Host "`n$AppName has been uninstalled." -ForegroundColor Green
    exit 0
}

Write-Host "Installing $AppName..." -ForegroundColor Cyan
Write-Host ""

# Check if we're running from the source directory
$SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = "$SourceDir\Notes\Notes.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Host "Error: Could not find Notes.csproj. Run this script from the Notes repository root." -ForegroundColor Red
    exit 1
}

# Stop running instance if any
Get-Process -Name "Notes" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Publish the application
Write-Host "Building and publishing application..." -ForegroundColor Yellow
$PublishDir = "$SourceDir\publish"

dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -o $PublishDir -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "  Build completed" -ForegroundColor Green

# Create installation directory
Write-Host "Installing to $InstallDir..." -ForegroundColor Yellow
if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Copy published files
Copy-Item "$PublishDir\*" $InstallDir -Recurse -Force
Write-Host "  Files copied" -ForegroundColor Green

# Create Start Menu shortcut
$StartMenuShortcut = "$StartMenuDir\$AppName.lnk"
Create-Shortcut -ShortcutPath $StartMenuShortcut -TargetPath $ExePath -Description "Notes - Quick note-taking app"
Write-Host "  Created Start Menu shortcut" -ForegroundColor Green

# Create Desktop shortcut
$DesktopShortcut = "$DesktopDir\$AppName.lnk"
Create-Shortcut -ShortcutPath $DesktopShortcut -TargetPath $ExePath -Description "Notes - Quick note-taking app"
Write-Host "  Created Desktop shortcut" -ForegroundColor Green

# Add to auto-start
Set-ItemProperty -Path $RegPath -Name $AppName -Value "`"$ExePath`""
Write-Host "  Added to auto-start" -ForegroundColor Green

# Clean up publish directory
Remove-Item $PublishDir -Recurse -Force

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  $AppName has been installed successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Installation directory: $InstallDir"
Write-Host "  Start Menu shortcut created"
Write-Host "  Desktop shortcut created"
Write-Host "  Auto-start on login enabled"
Write-Host ""
Write-Host "To uninstall, run: .\Install.ps1 -Uninstall" -ForegroundColor Yellow
Write-Host ""

# Ask if user wants to start the app now
$StartNow = Read-Host "Start Notes now? (Y/n)"
if ($StartNow -ne "n" -and $StartNow -ne "N") {
    Start-Process $ExePath
    Write-Host "Notes is starting..." -ForegroundColor Green
}
