# Stock & Flow — Release Checklist

Status legend: [x] done · [ ] still required · [~] partially done

## 1. App identity & versioning
- [x] Real app ID: `com.stockandflow.app` (change the reverse-DNS to your own domain if you own one — it's permanent on the stores).
- [x] Display name: "Stock & Flow".
- [x] Branded app icon + splash (replaces the default ".NET").
- [ ] Bump version each release in `StockAndFlow.Mobile.csproj`:
  - `ApplicationDisplayVersion` = marketing version (e.g. `1.0.0`)
  - `ApplicationVersion` = integer build number, **must increase every upload**.

## 2. Android (Google Play)
- [ ] Google Play Developer account ($25 one-time).
- [ ] Create a **signing keystore** (one-time) and back it up securely — losing it means you can never update the app:
  ```
  keytool -genkeypair -v -keystore stockandflow.keystore -alias stockandflow -keyalg RSA -keysize 2048 -validity 10000
  ```
- [ ] Build a signed `.aab`: set `SF_KEYSTORE/SF_KEYALIAS/SF_STOREPASS/SF_KEYPASS`, then run `build-release.cmd` (Windows) or `build-release.sh` (macOS/Linux).
- [ ] Play Console: create app, fill store listing (title, short/full description, screenshots — phone + 7" + 10" tablet, feature graphic 1024x500), content rating, data-safety form, privacy policy URL.
- [ ] Upload the `.aab` to Internal testing → Closed → Production.

## 3. iOS (App Store) — requires a Mac
- [ ] Apple Developer Program ($99/year).
- [ ] On a Mac: re-add `net10.0-ios` to `<TargetFrameworks>` in `StockAndFlow.Mobile.csproj` (removed so the project builds on Windows).
- [ ] Create App ID, distribution certificate, and provisioning profile.
- [ ] Build/sign the `.ipa` (see `build-release.sh` iOS section) and upload via Transporter / Xcode.
- [ ] App Store Connect: listing, screenshots (6.7" + 5.5" + iPad), privacy nutrition labels, review notes.
- [x] `NSPhotoLibraryUsageDescription` / `NSCameraUsageDescription` already in `Platforms/iOS/Info.plist`.

## 4. Engineering readiness
- [x] Tests: 36/37 pass (1 is an env-sensitive 10k-row perf threshold).
- [x] Release/trimmed Android build verified (Core rooted so EF Core + reflection survive trim/AOT).
- [x] Mobile: image picker, add/edit/delete, details, dashboard, charts, dark mode verified on emulator.
- [x] Invoice generation wired into the mobile Record Sale flow (writes to app cache + Share sheet).
- [x] Verified on emulator: **Excel Export → Share** (ClosedXML), **image picker** (system photo picker), dark mode.
- [ ] **On real hardware** (at least one Android phone, one iPhone): full smoke test.
- [ ] Verify on-device (not yet done): **Excel Import** round-trip, a **complete sale with stock**, **invoice PDF** renders (QuestPDF — wired with graceful fallback, but PDF rendering not yet confirmed on a device).
- [ ] First-run on a clean device (DB seeding, no data) looks correct.
- [ ] Security review of the mobile credential encryption (`MauiCredentialProtector`).

## 5. Known gaps / nice-to-have
- Drawer/tab icons are a fixed set; the drawer is a dark "sidebar" in both themes by design.
- Shopify sync is in Core but not surfaced/tested on mobile.
- WPF desktop app got the modern theme on the main window; secondary dialogs inherit it but weren't individually QA'd.

## Build commands
| Goal | Command |
|---|---|
| Debug run (Android, emulator/device attached) | `dotnet build StockAndFlow.Mobile -t:Run -f net10.0-android` |
| Signed Play bundle | `build-release.cmd` / `build-release.sh` (env vars set) |
| Run tests | `dotnet test StockAndFlow.Tests` |
| WPF desktop | `dotnet run --project StockAndFlow` |
