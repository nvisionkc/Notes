# Notes App Web Installer
# Install with: irm https://YOURSITE.com/notes/install.ps1 | iex
# Or: powershell -c "irm https://YOURSITE.com/notes/install.ps1 | iex"

$ErrorActionPreference = "Stop"
$AppName = "Notes"
$DownloadUrl = "https://YOURSITE.com/notes/Notes-Installer.zip"  # UPDATE THIS
$TempDir = "$env:TEMP\NotesInstaller"
$InstallDir = "$env:LOCALAPPDATA\Programs\Notes"
$ExePath = "$InstallDir\Notes.exe"
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$DesktopDir = [Environment]::GetFolderPath("Desktop")
$RegPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

function Create-Shortcut {
    param([string]$ShortcutPath, [string]$TargetPath, [string]$Description = "")
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $TargetPath
    $Shortcut.Description = $Description
    $Shortcut.WorkingDirectory = Split-Path $TargetPath
    $Shortcut.Save()
}

Write-Host ""
Write-Host "  _   _       _            " -ForegroundColor Yellow
Write-Host " | \ | | ___ | |_ ___  ___ " -ForegroundColor Yellow
Write-Host " |  \| |/ _ \| __/ _ \/ __|" -ForegroundColor Yellow
Write-Host " | |\  | (_) | ||  __/\__ \" -ForegroundColor Yellow
Write-Host " |_| \_|\___/ \__\___||___/" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Quick Note-Taking App" -ForegroundColor Cyan
Write-Host ""

# Stop running instance
Write-Host "Preparing installation..." -ForegroundColor Gray
Get-Process -Name "Notes" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Create temp directory
if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    # Download
    Write-Host "Downloading Notes..." -ForegroundColor Yellow
    $zipPath = "$TempDir\Notes.zip"

    $ProgressPreference = 'SilentlyContinue'  # Speed up download
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing
    $ProgressPreference = 'Continue'

    Write-Host "Extracting..." -ForegroundColor Yellow
    Expand-Archive -Path $zipPath -DestinationPath $TempDir -Force

    # Install
    Write-Host "Installing..." -ForegroundColor Yellow
    if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    # Copy from extracted app folder
    $appSource = "$TempDir\app"
    if (Test-Path $appSource) {
        Copy-Item "$appSource\*" $InstallDir -Recurse -Force
    } else {
        # Fallback if structure is different
        Copy-Item "$TempDir\*" $InstallDir -Recurse -Force -Exclude "*.ps1","*.bat","*.txt"
    }

    # Create shortcuts
    Create-Shortcut -ShortcutPath "$StartMenuDir\$AppName.lnk" -TargetPath $ExePath -Description "Notes - Quick note-taking app"
    Create-Shortcut -ShortcutPath "$DesktopDir\$AppName.lnk" -TargetPath $ExePath -Description "Notes - Quick note-taking app"

    # Auto-start
    Set-ItemProperty -Path $RegPath -Name $AppName -Value "`"$ExePath`""

    Write-Host ""
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Start Menu shortcut created" -ForegroundColor Gray
    Write-Host "  Desktop shortcut created" -ForegroundColor Gray
    Write-Host "  Auto-start enabled" -ForegroundColor Gray
    Write-Host ""

    # Start the app
    Write-Host "Starting Notes..." -ForegroundColor Cyan
    Start-Process $ExePath

} catch {
    Write-Host "Installation failed: $_" -ForegroundColor Red
    exit 1
} finally {
    # Cleanup
    if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host ""
Write-Host "To uninstall, run:" -ForegroundColor Gray
Write-Host "  Remove-Item '$InstallDir' -Recurse -Force" -ForegroundColor Gray
Write-Host "  Remove-Item '$StartMenuDir\$AppName.lnk' -Force" -ForegroundColor Gray
Write-Host "  Remove-Item '$DesktopDir\$AppName.lnk' -Force" -ForegroundColor Gray
Write-Host "  Remove-ItemProperty -Path '$RegPath' -Name '$AppName'" -ForegroundColor Gray
Write-Host ""
