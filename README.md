# Notes

A fast, lightweight note-taking app for Windows with clipboard history integration.

![Windows](https://img.shields.io/badge/Windows-10%2B-blue?logo=windows)
![.NET](https://img.shields.io/badge/.NET-10.0-purple?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Rich Text Editing** - Bold, italic, underline, lists, and headings
- **Clipboard History** - Automatically captures copied text (last 20 items)
- **Toast Notifications** - Click notifications to view captured clipboard content
- **Auto-Save** - Notes save automatically, never lose your work
- **System Tray** - Runs quietly in the background
- **Dark/Light Mode** - Follows your Windows theme
- **Keyboard Shortcuts** - Ctrl+S to save, Ctrl+N for new note

## Installation

### Quick Install (PowerShell)

```powershell
irm https://yoursite.com/notes/install.ps1 | iex
```

### Manual Install

1. Download the [latest release](https://github.com/nvisionkc/Notes/releases)
2. Extract the ZIP
3. Run `Install-Notes.bat`

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10 version 1903 or later

### Build

```bash
# Clone the repo
git clone https://github.com/nvisionkc/Notes.git
cd Notes

# Build
dotnet build Notes/Notes.csproj

# Run
dotnet run --project Notes/Notes.csproj
```

### Create Installer Package

```powershell
.\Build-Installer.ps1
```

This creates a distributable installer in the `installer/` folder.

## Project Structure

```
Notes/
├── Notes/                    # Main MAUI Blazor project
│   ├── Components/           # Blazor components
│   │   ├── Layout/          # MainLayout, Sidebar, ClipboardHistory
│   │   ├── Editor/          # RichTextEditor
│   │   └── Pages/           # Home page
│   ├── Data/                # Entity Framework models
│   ├── Services/            # Business logic
│   │   ├── NoteService.cs   # CRUD operations
│   │   ├── ClipboardService.cs  # Clipboard monitoring
│   │   ├── TrayService.cs   # System tray integration
│   │   └── ThemeService.cs  # Windows theme detection
│   └── wwwroot/             # Static assets
├── web-installer/           # Web distribution files
├── Install.ps1              # Local installer script
└── Build-Installer.ps1      # Creates distributable package
```

## Tech Stack

- **.NET MAUI Blazor Hybrid** - Cross-platform UI framework
- **Entity Framework Core** - SQLite database
- **Win32 Interop** - System tray, clipboard monitoring, toast notifications

## Screenshots

*Coming soon*

## License

MIT License - feel free to use and modify.

## Acknowledgments

Built with the help of [Claude Code](https://claude.ai/claude-code).
