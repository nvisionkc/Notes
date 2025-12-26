# Hosting Notes App for Distribution

## Quick Setup

1. **Upload these files to your website:**
   ```
   /notes/
   ├── index.html           # Landing page
   ├── install.ps1          # Web installer script
   └── Notes-Installer.zip  # App package (copy from parent folder)
   ```

2. **Update the URLs in these files:**

   **install.ps1** (line 6):
   ```powershell
   $DownloadUrl = "https://yoursite.com/notes/Notes-Installer.zip"
   ```

   **index.html** (line 85):
   ```html
   irm https://yoursite.com/notes/install.ps1 | iex
   ```

3. **Done!** Share the link with colleagues.

---

## How Colleagues Install

### Option 1: One-Line Install (Easiest)
Open PowerShell and run:
```powershell
irm https://yoursite.com/notes/install.ps1 | iex
```

### Option 2: Download and Install
1. Visit `https://yoursite.com/notes/`
2. Click "Download Installer"
3. Extract the ZIP
4. Run `Install-Notes.bat`

---

## MIME Types (if needed)

Some web servers need these MIME types configured:
- `.ps1` → `text/plain` or `application/octet-stream`
- `.zip` → `application/zip`

### IIS (web.config):
```xml
<configuration>
  <system.webServer>
    <staticContent>
      <mimeMap fileExtension=".ps1" mimeType="text/plain" />
    </staticContent>
  </system.webServer>
</configuration>
```

### Apache (.htaccess):
```apache
AddType text/plain .ps1
```

### Nginx:
```nginx
types {
    text/plain ps1;
}
```

---

## Updating the App

When you have a new version:
1. Run `Build-Installer.ps1` on your dev machine
2. Replace `Notes-Installer.zip` on your website
3. Colleagues run the install command again to update

---

## Security Notes

- The install script runs with user permissions (no admin needed)
- App installs to user's local AppData folder
- No system-wide changes are made
- Users can uninstall anytime
