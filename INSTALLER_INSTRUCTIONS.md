# How to Create an Installer for Stock & Flow

## Quick Start (2 Steps)

### Step 1: Install Inno Setup (One-time setup)

1. Download Inno Setup from: https://jrsoftware.org/isdl.php
2. Run the installer (choose default options)
3. That's it! Inno Setup is now installed.

### Step 2: Build the Installer

1. Open Command Prompt in this folder (`C:\Dev\StockAndFlow`)
2. Run: `build-installer.cmd`
3. Wait 2-3 minutes for the build to complete
4. Your installer will be at: `installer-output\StockAndFlow-Setup-1.0.0.exe`

**Done!** You now have a professional installer EXE that you can distribute.

---

## What the Installer Does

When users run `StockAndFlow-Setup-1.0.0.exe`, it will:

1. ✅ Show a professional installation wizard
2. ✅ Let users choose installation location (default: `C:\Program Files\Stock & Flow`)
3. ✅ Copy all application files
4. ✅ Create Start Menu shortcuts
5. ✅ Optionally create Desktop shortcut
6. ✅ Add to Windows Apps & Features (for easy uninstall)
7. ✅ Launch the app when installation completes

## Installer Features

- **Self-contained**: No .NET runtime required! Everything is bundled.
- **Single EXE**: Users only download one file (~80-100 MB)
- **Professional**: Uses Windows standard installer UI
- **Uninstaller**: Automatic uninstall support via Windows Settings
- **Admin rights**: Installs to Program Files (requires admin)

---

## Advanced: Customization

### Change Version Number

Edit `installer-setup.iss` line 6:
```
#define MyAppVersion "1.0.0"
```

Change to your version (e.g., "1.1.0", "2.0.0", etc.)

### Change Company Name

Edit `installer-setup.iss` line 7:
```
#define MyAppPublisher "Your Company Name"
```

### Change Website URL

Edit `installer-setup.iss` line 8:
```
#define MyAppURL "https://yourwebsite.com"
```

### Add Custom Icon

1. Create or download an `.ico` file (256x256 recommended)
2. Save it as `StockAndFlow\app-icon.ico`
3. The installer script already references this file

**Free icon resources:**
- https://icon-icons.com
- https://www.iconfinder.com
- https://icons8.com

---

## Testing the Installer

### Before Distribution:

1. **Test on your machine:**
   - Run the installer
   - Verify the app launches
   - Verify all features work
   - Uninstall via Windows Settings > Apps
   - Verify all files are removed

2. **Test on a clean Windows machine:**
   - Virtual machine or friend's computer
   - Install and verify it works
   - This ensures all dependencies are included

3. **Antivirus check:**
   - Upload to VirusTotal.com (optional)
   - Some antivirus may flag unknown installers (false positive)

---

## Distribution

### Option 1: Direct Download
- Host the EXE on your website
- Share via Google Drive, Dropbox, etc.
- Email to customers

### Option 2: Gumroad (Recommended for Selling)
- Upload to Gumroad.com
- Set your price
- Automatic payment processing
- License key generation

### Option 3: Your Own Website
- Create download page
- Use Stripe for payments
- Generate license keys manually or with a service

---

## File Size Expectations

- **Installer EXE**: ~80-100 MB
- **Installed size**: ~150-200 MB

The installer is large because it's **self-contained** - it includes:
- Your application code
- .NET 10 runtime
- SQLite database engine
- All dependencies (LiveCharts, Serilog, etc.)

**This is good!** Users don't need to install .NET separately.

---

## Troubleshooting

### Error: "Inno Setup not found"
- Download from: https://jrsoftware.org/isdl.php
- Install to default location: `C:\Program Files (x86)\Inno Setup 6`

### Error: "Build failed"
- Close the running app (if open)
- Try: `dotnet clean` then run `build-installer.cmd` again

### Error: "Cannot find app-icon.ico"
- Edit `installer-setup.iss` and comment out line 25 (add semicolon):
  ```
  ; SetupIconFile=StockAndFlow\app-icon.ico
  ```
- Or create a simple icon file at that location

### Installer is too large
- Already optimized with ReadyToRun compilation
- Size is normal for self-contained .NET apps
- Alternative: Require users to install .NET 10 separately (smaller installer, worse UX)

---

## Next Steps After Creating Installer

### For Beta Testing:
1. Build installer with `build-installer.cmd`
2. Upload to Google Drive or Dropbox
3. Share link with 5-10 beta testers
4. Collect feedback

### For Launch:
1. Update version to 1.0.0 (already set!)
2. Create professional icon (256x256 .ico file)
3. Test installer on 2-3 clean Windows machines
4. Upload to Gumroad, your website, or Product Hunt
5. Market to Shopify sellers!

---

## Building Without Inno Setup (Alternative)

If you don't want to use Inno Setup, you can just distribute the published folder:

```cmd
dotnet publish StockAndFlow/StockAndFlow.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true
```

Then ZIP the folder at:
`StockAndFlow\bin\Release\net10.0-windows\win-x64\publish`

**Pros:** No installer needed, simple
**Cons:** Users must manually extract ZIP, less professional

---

## License Key System (Future Enhancement)

To add license key validation:

1. Add a license check in `App.xaml.cs` startup
2. Show license entry dialog if invalid
3. Validate against your license server or local algorithm
4. Use services like:
   - Gumroad (built-in license keys)
   - Paddle (payment + licensing)
   - Keygen.sh (just licensing)

---

## Questions?

Common questions answered:

**Q: Can I distribute this for free?**
A: Yes! Just build and share the installer.

**Q: Can I sell this?**
A: Yes! Set up Gumroad or Stripe and start selling.

**Q: Do users need .NET installed?**
A: No! The installer is self-contained with everything included.

**Q: Can I update the app after users install it?**
A: Yes, create a new installer with updated version number. Consider adding auto-update feature later.

**Q: Is code signing required?**
A: Not required, but recommended for professional distribution. Prevents Windows "Unknown Publisher" warnings. Costs ~$100-300/year for a code signing certificate.

---

Good luck with your launch! 🚀
