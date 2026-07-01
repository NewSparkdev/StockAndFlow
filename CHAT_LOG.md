# Stock & Flow — Project Work Log

> Running log of what's been done on this project, so it can be referenced later.
> Branch for all this work: **`maui-migration`** (base: `main`).
> Last updated: **2026-07-01**.

---

## 1. The big picture — what this project became

**Stock & Flow** started as a **Windows-only WPF** inventory-management app (inventory,
sales, expenses, reports, Shopify sync, PDF invoices, Excel export).

The goal of this effort: **turn it into a mobile app for iPhone + Android while keeping the
Windows desktop app working** — from a single shared codebase.

**Approach chosen:** extract the shared logic into a portable **`StockAndFlow.Core`** library,
then add a **.NET MAUI** app (`StockAndFlow.Mobile`) for iOS/Android/Windows alongside the
existing WPF app. Both apps reference the same Core.

### Solution layout now
| Project | Target | Role |
|---|---|---|
| `StockAndFlow.Core` | `net10.0` | Models, Services, ViewModels, EF Core + SQLite data layer, platform abstractions. **All shared logic.** |
| `StockAndFlow` | `net10.0-windows` | The original **WPF desktop** app (now UI-only, references Core). |
| `StockAndFlow.Mobile` | `net10.0-android` (+ `-windows`) | The new **.NET MAUI** app. iOS target removed so it builds on Windows — re-add on a Mac. |
| `StockAndFlow.Tests` | `net10.0-windows` | xUnit tests. |

---

## 2. Status at a glance (2026-06-20)

| Area | Status |
|---|---|
| Core extracted, WPF re-wired, full solution builds | ✅ Done |
| MAUI app builds + runs on Android emulator | ✅ Done |
| All mobile screens (dashboard, inventory, sales, expenses, reports, settings, export/import) | ✅ Done |
| Modern UI (light/dark, design tokens, adaptive) — mobile **and** WPF | ✅ Done |
| Record-sale → **customer invoice (PDF)** → Share | ✅ **Works on mobile + desktop** |
| Excel export → Share | ✅ Verified on emulator |
| Image picker | ✅ Verified on emulator |
| Release/trimmed Android build | ✅ Verified |
| App identity: id, name, branded icon/splash | ✅ Done |
| Tests | ✅ 38/39 pass (1 env-sensitive perf timing) |
| **iOS build + on-device test** | ⛔ Needs a Mac (see §6) |
| Real-hardware QA, store accounts/listings | ⛔ Needs user resources |

---

## 3. Commit history on this branch (newest first)

| Commit | What it did |
|---|---|
| `4354b3f` | **Fix mobile invoices: render PDFs with SkiaSharp instead of QuestPDF** |
| `1852ec8` | Update release checklist: export + image picker verified on emulator |
| `541f690` | Invoice-on-mobile wiring, dark sidebar drawer, release scaffolding |
| `7dfcc3b` | Phase 3: real app identity, branded icon/splash, iOS permission strings |
| `2a2ac38` | Make Release/trimmed builds correct: root `StockAndFlow.Core` for the trimmer |
| `74cca87` | Fix LoadTests teardown: clear SQLite pool before deleting temp db |
| `6e5aa06` | Fix list-item tap-to-details and stray selection highlight |
| `5fb0a81` | Modernize the flyout drawer (Material-3 style) |
| `63c9045` | Modernize WPF desktop UI to match the mobile app's design language |
| `f0b6f31` | Modern, adaptive UI: design tokens, metric-tile dashboard, dark mode |
| `8635420` | Deferred trio: numeric input, swipe actions, dark-mode theming |
| `590f17e` | QOL pass: correctness fixes + mobile UX gaps |
| `91cc4eb` | Opt out of Android 15 edge-to-edge enforcement (fix status-bar overlap) |
| `2b3d7f4` | Make Android status-bar icons light so they're legible on the purple bar |
| `13b4e2d` | Fix list-row selection highlight bleeding past the card border |
| `cf727b8` | **Add .NET MAUI mobile app sharing a Core library with the WPF app** |
| `38d67b0` | Initial commit: Stock & Flow inventory management system |

*(Branch totals vs `main`: ~167 files changed, ~11,245 insertions, ~999 deletions.)*

---

## 4. What was done, in detail (by theme)

### 4.1 Shared-library extraction (Phase 0)
- Created `StockAndFlow.Core` (`net10.0`) holding all Models, Services, ViewModels, and the
  EF Core + SQLite data layer.
- Introduced **platform abstractions** (namespace `StockAndFlow.Platform`): `IDialogService`,
  `IFilePickerService`, `IPathProvider`, `ICredentialProtector`, `IEditorPresenter`, plus a static
  `UiDispatcher` for cross-thread UI marshaling. WPF and Mobile each supply their own implementations.
- Replaced WPF's `CommandManager`-based command re-query (Windows-only) with a portable
  `RelayCommand`/`IRelayCommand` + reflection-based re-query in `ViewModelBase`.

### 4.2 The MAUI mobile app (Phases 1–2)
- New `StockAndFlow.Mobile` MAUI project; DI via `Microsoft.Extensions.DependencyInjection`.
- **All screens built**: metric-tile dashboard, inventory/sales/expenses/adjustments lists
  (swipe edit/delete, tap-for-details, search, date filters, low-stock badge), add/edit/detail
  pages, record-sale cart, business settings, export/import, and LiveCharts charts on Reports.
- **Navigation**: hamburger **flyout drawer** (dark "sidebar" style) — chosen over a bottom tab
  bar because 6 sections overflowed Android's 5-slot bar into an ugly "More". (A later hybrid kept
  bottom tabs for the top 5 + the drawer.)
- Startup hardened: DB init moved off the UI thread behind a loading page (was causing an ANR).

### 4.3 Modern UI / dual-app design (mobile + desktop)
- Shared design language: brand purple `#512BD4`, surface tokens, rounded cards, light/dark via
  `AppThemeBinding`. Adaptive layouts.
- WPF desktop got a matching `ModernTheme.xaml` (restyled window, buttons, inputs, DataGrid, tabs).

### 4.4 Deployment-readiness
- **Release/trimmed Android build fixed**: `<TrimmerRootAssembly Include="StockAndFlow.Core"
  RootMode="all" />` so EF Core + the reflection-based command re-query survive trimming/AOT.
- App identity: ApplicationId **`com.stockandflow.app`**, title **"Stock & Flow"**, branded
  icon + splash (bars + arrow glyph, replaced the default ".NET"), iOS photo/camera usage strings.
- Release scaffolding: `build-release.cmd` / `build-release.sh` (signed `.aab` via env-var
  keystore secrets), `RELEASE_CHECKLIST.md`, `MOBILE_MIGRATION_PLAN.md`, `global.json` (SDK pin).
- **Secrets safety**: `.gitignore` excludes keystores/certs (`*.keystore`, `*.jks`, `*.p12`,
  `*.mobileprovision`, `*.cer`, …); signing secrets are passed via env vars
  (`SF_KEYSTORE/SF_KEYALIAS/SF_STOREPASS/SF_KEYPASS`), never committed.

### 4.5 Customer invoices — fixed for mobile (2026-06-20, commit `4354b3f`)
- **Problem found on the emulator:** invoice generation crashed/failed on Android. Root cause:
  **QuestPDF ships its own native Skia build (`libQuestPdfSkia.so`) that depends on
  `libstdc++.so.6`**, which isn't in Android's linker namespace → `dlopen` fails. **QuestPDF is
  desktop/server-only and cannot run on Android/iOS.**
- **Fix:** removed QuestPDF entirely; rewrote `InvoiceService` to render PDFs with
  **SkiaSharp `SKDocument.CreatePdf`**. SkiaSharp's `libSkiaSharp` is already present on every
  head (it renders the LiveCharts charts), so **one renderer now serves Windows + Android + iOS**.
- Also improved the invoice totals to show a proper **Subtotal / Tax / Total** breakdown
  (the old version showed subtotal == total).
- **Verified end-to-end on the Android emulator:** business settings → stocked item → record sale
  → "Generate invoice? Yes" → 55 KB PDF generated in app cache → **Share sheet opened**. Pulled
  the PDF off the device and confirmed correct, professional layout with **selectable text**.
- Added `InvoiceServiceTests` (2 tests, passing) covering desktop rendering.

### 4.6 Branding polish — icon, splash & loading screen (2026-07-01)
- **Root-caused a "shows .NET" report on the emulator:** the app on the device was a *stale build
  under the default template id* `com.companyname.stockandflow.mobile` (generic ".NET" launcher
  icon). The current build uses `com.stockandflow.app` with the branded icon, so a plain redeploy
  would install *alongside* the old one. Fix = uninstall the old package, `rm -rf bin obj` (so the
  resizetizer regenerates mipmaps), rebuild. Icon source (`Resources/AppIcon/appiconfg.svg`, bars +
  arrow on `#512BD4`) was already correct — this was purely a stale-install / launcher-icon cache.
- **Sharpened the growth arrow** in the logo glyph: the old arrowhead was a small crude triangle
  not aligned to the line direction, drawn at `opacity="0.85"` (which read as blunt/faint). Replaced
  with a properly aligned, full-opacity arrowhead (shaft trimmed so the point forms a clean tip).
  Applied to both `Resources/AppIcon/appiconfg.svg` and `Resources/Splash/splash.svg`.
- **Branded the DB-init loading page** (`App.xaml.cs` → `BuildLoadingPage`): was a bare spinner +
  "Loading Stock & Flow…" label; now a centered logo (128×128) + bold "Stock & Flow" wordmark +
  spinner. New `Resources/Images/logo.svg` (glyph cropped to a square viewBox) flows through the
  `MauiImage` pipeline like the tab icons.
- **Native splash** (`Resources/Splash/splash.svg`) already had the glyph but with the old blunt
  arrow — updated to the sharpened arrow so branding is consistent across all three cold-start
  moments: **native splash → loading page → launcher icon**.
- Emulator-verified all three (screenshots): sharp logo on the native splash (no blank-purple
  flash), the branded loading page, and "Stock & Flow" + branded icon in the app drawer.
- **Drawer flyout header** (`AppShell.cs` → `BuildHeader`): replaced the "S&F" text avatar with the
  logo in a rounded app-tile badge (60px, subtle ring), and reworked the layout — was a 184px header
  with everything jammed at the bottom (dead space up top); now a 150px header with the badge +
  "Stock & Flow" title + "Inventory & sales" subtitle in a horizontal row, centered.

### 4.7 Settings restructure + Export/Import UX (2026-07-01)
- **Problem:** tapping the dashboard "Settings" toolbar item opened the Business-info form directly;
  it should be a menu. **Fix:** new `Pages/SettingsPage.xaml(.cs)` — a settings menu with grouped,
  tappable rows (icon chip + title + subtitle + chevron), themed with the shared tokens
  (`SurfaceLight/Dark`, `OnSurface`, `ListCard`) for light/dark. Sections: **BUSINESS** → Business
  Settings, **DATA** → Export & Import, **ABOUT** → About (shows `AppInfo` version). `MainPage`'s
  Settings toolbar now opens this menu; each sub-page is pushed as its own modal (Export/Import is
  also reached from Reports, so its open/close was left untouched).
- **Row icons:** started as emoji, then swapped for **monochrome white SVGs** on solid `#512BD4`
  tiles (`settings_business.svg`, `settings_data.svg`, `settings_about.svg`) — a single-color SVG
  can't contrast both the light and dark chip backgrounds, so white-on-purple (matching the drawer
  badge / app icon) reads cleanly in both themes.
- **Export/Import "no way back" report:** tapping *Import from Excel* opens Android's system file
  picker (Google's DocumentsUI), which lands on an empty "Recent / No items" screen with **no
  on-screen Cancel** — users felt stranded. The system back gesture *does* return to the app
  (verified: focus returns to `MainActivity`), but it's not discoverable, and **an app cannot add a
  Cancel button to the system picker.** Mitigations in `Pages/ExportImportPage.xaml(.cs)`:
  - **File-type filter** (`FilePickerFileType`, xlsx MIME) so the picker browses spreadsheets only.
  - **Export now saves to a predictable `Downloads/Stock & Flow` folder** (Android 10+ via MediaStore
    — no permission; Windows via `%USERPROFILE%\Downloads`; iOS/older falls back to the Share sheet),
    and shows an "Export saved" dialog with the location + **Share**/**Done** options.
  - **Import hint text** telling users to use the picker's ☰ menu → Downloads › Stock & Flow, and
    that **Back** cancels and returns.
  - Emulator-verified the full round-trip: export writes to `/sdcard/Download/Stock & Flow/…xlsx`,
    and that file is then findable in the import picker.

---

## 5. Bugs found & fixed during emulator testing
1. **Startup ANR** — blocking `IDataService.InitializeAsync().GetAwaiter().GetResult()` on the UI
   thread → async init behind a loading page.
2. **Save button stuck disabled** — portable `RelayCommand` dropped WPF's `CommandManager`
   re-query → `ViewModelBase.OnPropertyChanged` now re-queries `IRelayCommand` properties
   (reflection, cached).
3. **Status bar cut off / overlapped** — Android 15 edge-to-edge enforcement + dark icons on the
   purple bar → opt out of edge-to-edge + light status-bar icons; switched to a better emulator
   skin (`medium_phone`).
4. **Orange selection highlight behind cards + couldn't open details** — list `SelectionMode`
   set to `None`; tap handled by gesture; fixed selection bleed past the card border.
5. **QuestPDF crash on Android** — see §4.5 (ultimately removed QuestPDF for SkiaSharp).
6. **LoadTests teardown IOException** — SQLite file lock → `SqliteConnection.ClearAllPools()` +
   GC + tolerant delete before removing the temp db.

---

## 6. Open items / what's NOT done yet

### Needs a Mac (Apple requirement — Xcode is macOS-only)
- iOS build: re-add `net10.0-ios` to `<TargetFrameworks>` in `StockAndFlow.Mobile.csproj` on a Mac.
- On-device iOS test of the app (the invoice uses the same SkiaSharp path as Android, which works).

### iOS testing routes discussed (no decision made yet)
- **GitHub Actions (free macOS runners) + TestFlight** — cloud build, install over-the-air. The
  closest equivalent to "Expo's EAS Build + Expo Go" for this .NET stack. *(Recommended.)*
- **Rent a cloud Mac** (MacStadium / MacinCloud / AWS EC2 Mac) for a one-off build.
- **Borrow/buy a cheap Mac** as a build host.
- **Free-but-clunky:** free Apple ID "personal team" provisioning installs to your own device, but
  needs a Mac + cable and the app **expires every 7 days**; AltStore/Sideloadly can install an
  `.ipa` from Windows (also 7-day refresh) but producing the `.ipa` still needs a Mac once.
- **Cost note:** the **Apple Developer Program ($99/yr)** is required for TestFlight, cloud-build
  installs, and the App Store — and is unavoidable to publish/sell the app anyway.
- **Note:** **Expo does NOT apply** — it's React-Native/JavaScript only; using it would mean
  rewriting the entire C# app from scratch. Not worth it.

### Needs user resources / accounts
- Google Play Developer account ($25 one-time) + signing keystore (back it up!).
- Apple Developer Program ($99/yr).
- Store listings: screenshots, descriptions, feature graphic, content rating, privacy policy.
- Real-hardware smoke test (Android phone + iPhone/iPad).
- On-device verification of: Excel **import** round-trip; iOS invoice render.
- Security review of the mobile credential encryption (`MauiCredentialProtector`, AES key in
  SecureStorage).

---

## 7. How to build / run / test

| Goal | Command |
|---|---|
| WPF desktop | `dotnet run --project StockAndFlow` |
| Android (emulator/device attached) | `dotnet build StockAndFlow.Mobile -t:Run -f net10.0-android` |
| Run tests | `dotnet test StockAndFlow.Tests` |
| Signed Play bundle | set `SF_*` env vars, then `build-release.cmd` / `build-release.sh` |

**Emulator on this machine:** AVD `sf_phone` / `sf_pixel` (API 35) in the user SDK at
`%LOCALAPPDATA%\Android\Sdk`; JDK at `C:\Program Files\Android\openjdk\jdk-21.0.8`. The VS build
SDK lives at `C:\Program Files (x86)\Android\android-sdk`; `adb` is in its `platform-tools`.

---

## 8. Key technical decisions (and why)
- **Shared Core + two heads** (not a rewrite) — keeps all logic in one place; WPF and MAUI are thin.
- **MAUI** (not React Native/Expo/Flutter) — reuses the existing C#/.NET investment.
- **SkiaSharp for PDF** (not QuestPDF) on mobile — QuestPDF's native lib can't load on Android/iOS;
  SkiaSharp is cross-platform and already in the app.
- **iOS TFM removed** so the solution builds on Windows day-to-day; re-add on a Mac for iOS.
- **Trimmer roots Core** so reflection (commands) + EF Core survive Release/AOT.
- **No secrets in git** — keystores/certs git-ignored; signing via env vars.

---

## 9. Reference docs in the repo
- `MOBILE_MIGRATION_PLAN.md` — the migration plan + what's portable.
- `RELEASE_CHECKLIST.md` — store-release checklist with status.
- `BUSINESS_PLAN.md` — product/business context.
- `build-release.cmd` / `build-release.sh` — signed build scripts.
- `README.md` — project overview (tech stack updated: SkiaSharp for PDF).
