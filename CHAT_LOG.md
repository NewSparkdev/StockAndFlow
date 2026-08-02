# Stock & Flow — Project Work Log

> Running log of what's been done on this project, so it can be referenced later.
> Branch for all this work: **`maui-migration`** (base: `main`).
> **Releases ship from `worktree-onboarding`** (checked out at `.claude/worktrees/onboarding`) —
> it carries the CI workflows, version number, EF compiled model, and all `v*` tags.
> Last updated: **2026-08-02**.

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

## 2. Status at a glance (2026-08-01)

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
| Tests | ✅ 72/72 unit tests pass (perf suite excluded from CI) |
| Customer management (add/edit/select on sale) | ✅ Done |
| Sales-tax default, low-stock alerts | ✅ Done |
| Barcode scanning (camera, ZXing.Net) — mobile + WPF USB | ✅ Done |
| Bill of Materials (BOM / recipe) — WPF + mobile | ✅ Done |
| Friendlier field wording + tappable "?" help on inventory setup | ✅ Done (v1.1.3) |
| Track items by weight/volume (oz, lb, g, kg, …) with decimal stock | ✅ Done (v1.1.3) |
| "Your cost" auto-calculated from BOM + separate extra-costs field | ✅ Done (v1.1.4) |
| Unit test suite (BOM cascade, adjustments, sales-by-weight, DB migration) | ✅ 72 tests passing |
| GitHub Actions CI — unit tests on every push | ✅ Added, passing |
| GitHub Actions CI — Android (Google Play internal) | ✅ Passing (v1.1.4) |
| GitHub Actions CI — iOS (TestFlight) | ✅ Passing (v1.1.4) |
| Android testers (5) — Google Play internal | ✅ All active, v1.1.4 available |
| iOS testers (4) — TestFlight | ✅ v1.1.4 uploaded |
| Real-hardware QA, store accounts/listings | ⛔ Needs user resources |

---

## 3. Commit history on this branch (newest first)

| Commit | What it did |
|---|---|
| `21d66af` | Fix Shopify sync (was silently broken: PascalCase parsing of snake_case JSON = no-op syncs, no config UI, sunset API version, no pagination, order dedup never worked) + Shopify settings UI on both platforms. ✅ **Live-verified against a real dev store 2026-08-02** (see §4.18) |
| `897342b` | Invoices: render business logo + per-field display toggles (**⚠ entity changed — regen compiled model on onboarding before next release**) |
| `95f9e89` | Fix upgrade crash for pre-customer DBs: migrate `Sales.CustomerId` + `Customers` table |
| `60a9dfb` | Harden credential encryption: AES-GCM + "enc1:" marker, resilient key init, mobile migration, backup exclusions |
| `30bd0db` | WPF parity: unit-of-measure picker, extra-costs field, BOM cost breakdown |
| `e8e759c` | *(onboarding)* Merge BOM cost auto-calc; regenerate compiled model; bump to **v1.1.4** |
| `327b3ab` | Auto-calculate "Your cost" from BOM, with separate extra-costs field |
| `48fdb6f` | Add database migration tests (were silently excluded by `Data/` gitignore rule) |
| `7f93688` | Measure-by-weight option, friendlier labels + "?" help icons, test suite, tests CI |
| `a2c60c3` | *(onboarding)* Regenerate stale EF compiled model; align EF 10 packages; bump to **v1.1.3** |
| `56406cd` | Add Bill of Materials (BOM) feature for inventory items |
| `aaf7bcb` | iOS workflow: always build on workflow_dispatch (manual trigger bypass) |
| `8784c05` | iOS workflow: skip macOS build when only CI/version files changed |
| `327a38a` | Fix Android CI: add jarsigner signing step; bump to v1.1.2 |
| `f965afc` | Fix BOM details page crash on Android: marshal UI to main thread |
| `1f85362` | BOM dark mode: fix invisible text on details page + edit picker/entry |
| `e8acbd1` | BOM UX: invisible item text, huge layout, non-functional Add button (iOS + Android) |
| `fd22345` | Fix BOM: save crash, cross-thread collection crash, invisible item text |
| `808cee8` | Bump version to 1.0.7; add GitHub Actions Android + iOS CI workflows |
| `baaacf6` | Fix Settings modal and customer card visibility on Android |
| `b787f1e` | WPF: add USB barcode scanner support to Record Sale dialog |
| `444eb3d` | Barcode scanning: real camera + ZXing.Net decode via MediaPicker |
| `91a6997` | Phase 1: customers, sales-tax default, low-stock alerts, barcode scaffold |
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

### 4.8 Customer management + sales-tax + low-stock alerts (2026-07-xx, commit `91a6997`)
- **Customer model** added to Core: name, email, phone, address. Full CRUD via `CustomerService`.
- **Record Sale** now lets users attach a customer to a sale (picker, optional). Customer name
  appears on generated invoices.
- **Sales-tax default** in Business Settings — persisted, applied automatically to new sales.
- **Low-stock alerts** badge on the dashboard tile when any item is below `MinimumStockLevel`.
- **Barcode scaffold** added as groundwork for the scan feature (§4.9).
- WPF and MAUI both updated; Android-specific fix for customer card visibility and Settings modal
  (commit `baaacf6`).

### 4.9 Barcode scanning (2026-07-xx, commits `444eb3d`, `b787f1e`)
- **Mobile (Android + iOS):** real camera capture via `MediaPicker.CapturePhotoAsync`; image
  decoded with **ZXing.Net** (`BarcodeReader`). Scanned value populates the SKU/Barcode field on
  Add/Edit Inventory and pre-fills search on Record Sale.
- **WPF desktop:** USB HID barcode scanner support. Scanners appear as keyboards; a `PreviewKeyDown`
  listener on the Record Sale window collects rapid keystrokes (< 50 ms apart) and fires the scan
  handler when `Enter` arrives, bypassing normal text input.

### 4.10 Bill of Materials / Recipe feature (2026-07-xx, commits `fd22345`–`f965afc`)
A "finished good" inventory item can now declare which other items it consumes per unit sold
(e.g. "Box of 12" deducts 12 units of the raw component).

**Data layer (`StockAndFlow.Core`):**
- `BomComponent` model: `FinishedItemId`, `ComponentItemId`, `QuantityPerUnit`.
- `BomService.GetComponentsForItemAsync` / `SaveComponentsAsync`.
- `SQLiteDataService._tableNames` extended with `{ typeof(BomComponent), "BomComponents" }` —
  this was missing on first release, causing every BOM save/load to throw.

**ViewModel (`AddEditInventoryViewModel`):**
- `AvailableComponents` (all items except itself), `BomComponents` (current list), `PendingComponent`,
  `PendingQty`, `AddBomComponentCommand`, `RemoveCommand` on each entry.
- Collection mutations wrapped in `UiDispatcher.Run()` — without this, Android crashed on
  cross-thread `ObservableCollection` writes after `await`.

**WPF (`AddEditInventoryDialog.xaml` / `InventoryDetailsDialog.xaml.cs`):**
- BOM section in the edit dialog: ComboBox + qty TextBox + "Add" button + `ItemsControl` list.
- Details dialog shows the BOM components list.

**MAUI (`AddEditInventoryPage.xaml/.cs` + `InventoryDetailsPage.xaml/.cs`):**

Several bugs hit and fixed during iOS + Android testing:

| Bug | Root cause | Fix |
|---|---|---|
| Huge empty BOM area | `CollectionView` inside `ScrollView` takes infinite height | Replaced with `VerticalStackLayout` + `BindableLayout` |
| Add button did nothing on iOS | `Picker.SelectedItem` two-way binding unreliable when `ItemsSource` loads async; `CanExecute` stayed false | `SelectedIndexChanged` + `Clicked` code-behind; bypass Command binding |
| BOM item names invisible (add page) | `TextColor` not set; background forced white | Explicit `TextColor="#212121"` on labels |
| Dark mode: Picker/Entry text invisible | `BackgroundColor="White"` set explicitly; iOS dark mode kept text white | Removed `BackgroundColor`; kept `TextColor="#212121"` |
| Dark mode: BOM item names invisible (details page) | `BomDisplayRow` private record stripped by iOS IL trimmer; `QuantityDisplay` had hardcoded dark-blue colour | Rebuilt rows imperatively in code-behind (no DataTemplate binding); adaptive `qtyColor` (#3949AB light / #9FA8DA dark) |
| Android crash on details page | `BomList.Add()` and `IsVisible` called from thread-pool thread after `await` | Wrapped all View mutations in `MainThread.BeginInvokeOnMainThread()` |

### 4.11 GitHub Actions CI/CD (2026-07-22–23)
Two workflows added to the `worktree-onboarding` branch, triggered on `v*` tags:

**`android-build.yml`** (Ubuntu runner, ~4 min):
- `dotnet publish -f net10.0-android -c Release` with `AndroidKeyStore=true` + secrets.
- Explicit `jarsigner` signing step after locating the AAB — `dotnet publish` produces an
  unsigned intermediate `.aab`; the `find` command picked that up, causing Google Play to
  reject it as unsigned. Fix: sign in-place with `jarsigner` before upload.
- Uploads to **Google Play Internal Testing** via `r0adkll/upload-google-play@v1`.
- Build number = `github.run_number + 10`.

**`ios-build.yml`** (macOS runner, ~35 min):
- Imports P12 certificate + provisioning profile from secrets into a temp keychain.
- `dotnet publish -f net10.0-ios -c Release -r ios-arm64 -p:ArchiveOnBuild=true`.
- Uploads IPA to **TestFlight** via `fastlane pilot`.
- **Smart skip gate (added 2026-07-23):** a cheap Ubuntu `check` job runs first; if the only
  changes since the last tag are workflow files (`.yml`) or the csproj version bump (no `.cs`,
  `.xaml`, or resource files changed), the macOS `build` job is skipped entirely. This prevents
  spurious 35-minute TestFlight builds when only Android CI is being fixed.
- `workflow_dispatch` always bypasses the skip gate and forces a build.

**Version history:**
| Tag | Notable change |
|---|---|
| v1.0.4–v1.0.6 | Initial CI runs / signing setup |
| v1.0.7 | First public CI run |
| v1.0.8 | BOM save crash + cross-thread fix |
| v1.0.9 | BOM UX (layout, item text, Add button) |
| v1.1.0 | Dark mode BOM text fixes |
| v1.1.1 | Android details page threading fix |
| v1.1.2 | Android CI: jarsigner signing fix |

### 4.12 Tester onboarding — Android & iOS (2026-07-24)

**Android (Google Play internal testing):**
- Added 5 testers to the "Internal Testers" email list in Play Console:
  `Mutilatedteddy@gmail.com`, `j1gamer4life@gmail.com`, `josephsnyder16@gmail.com`,
  `luvolanetwork@gmail.com`, `walterquirinzierer@gmail.com`.
- Opt-in link: `https://play.google.com/apps/internaltest/4701554811649357203`
- Key gotcha: Play Console does NOT email the link — testers must open it on Android while
  signed into the listed Gmail account in the Play Store. Propagation can take up to ~1 hour
  after a new address is added.
- Investigated "try again" error: root cause was propagation delay, not a config issue. All 5
  eventually gained access after waiting.
- Briefly unchecked the email list (to test open access) — this set the track to **Inactive**
  (no testers = no access). Immediately restored. Lesson: the email list IS the access control
  for internal testing; removing it kills the track.

**iOS (TestFlight):**
- Internal Testers group in App Store Connect has 4 members:
  - `j.jointer@me.com` (Jason Jointer) — Installed 1.1.2 (40)
  - `alwayzsmilin00@gmail.com` (Shamika Jeffery) — Installed 1.1.2 (40)
  - `ebbygirl05@yahoo.com` (Ebone Ridley) — Invited (pending acceptance)
  - `bakerjustin171@gmail.com` (Justin Baker) — Invited (pending acceptance)
- Build #30 was triggered via `workflow_dispatch` (not a tag push) to avoid spurious Android CI
  trigger. v1.1.0 and v1.1.1 iOS builds were both killed by the `cancel-in-progress` concurrency
  group when the next tag was pushed, leaving TestFlight stuck at v1.0.9.
- TestFlight internal testing is email-only — no public link or redemption code. Apple sends from
  `no_reply@email.apple.com`; the email does not show the developer's personal address.

### 4.13 Tester-feedback UX: friendlier wording + tap-for-help (2026-08-01, shipped in v1.1.3)
- Testers said inventory setup was confusing — "cost/unit vs sale price vs quantity vs min stock
  level". Renamed everywhere on mobile: **Your cost (each)**, **Selling price (each)**,
  **How many in stock**, **Low stock alert** (and "Profit on each one sold").
- New reusable `HelpIcon` control (`StockAndFlow.Mobile/Controls/HelpIcon.cs`) — small tappable
  "?" circle (theme-aware, accessible) that opens a native alert with a plain-language
  explanation + concrete example. Added to all four fields (and later the measurement picker
  and extra-costs field). Help text lives in the ViewModel so it adapts to the item's unit.

### 4.14 Measure items by weight/volume (2026-08-01, shipped in v1.1.3)
- Candle-business testers wanted e.g. **90 oz of wax** instead of a count. New per-item
  **"How do you measure this item?"** picker: by count (default) or oz / lb / g / kg / fl oz /
  ml / L. Labels, help text, prices ("$0.30 / oz"), and low-stock alerts become unit-aware.
- **Quantities are now `decimal` end-to-end** (stock on hand, min level, sale quantity,
  adjustments, cart, reports, Excel) — fractional amounts like 2.5 oz work everywhere.
  `UnitOfMeasure` column auto-migrated (`DEFAULT 'each'`); old integer data reads back exactly.
- Fixed on the way: BOM deductions were **rounded to whole numbers** (a candle using 2.5 oz
  deducted 3 oz per sale). Now exact.
- BOM policy decision: component deductions **always follow a recorded sale**, going negative
  rather than silently skipping when stock runs short (`AdjustQuantityAsync` gained
  `forceAllowNegative`); and **editing a sale now cascades the quantity delta to BOM
  components** (previously only the finished good was adjusted).

### 4.15 Automated testing + the bugs it immediately caught (2026-08-01)
- Test project had been **broken since the BOM commit** (constructor change, never compiled)
  — nothing ran it. Fixed, then expanded 33 → **72 tests**: BOM cascade/restore, adjustment
  record/delete reversal, low-stock alert edges, selling by weight, sale-edit inventory deltas
  (drives the real `EditSaleViewModel`), and **real-file SQLite migration tests** (legacy
  schema → migrate → verify every value survives; fractional round-trip; idempotency).
- New CI workflow `.github/workflows/tests.yml`: runs the suite on **every push** to any
  branch (windows-latest; builds only the test chain, no MAUI workloads; Performance excluded).
- **The migration tests caught a shipping data-corruption bug on the release branch**: the EF
  **compiled model** (added for the iOS AOT startup fix; `UseModel(...)` bypasses
  `OnModelCreating`) was stale — it still had `int` quantities (90.5 oz stored as **90**) and
  was **missing the BomComponent entity entirely**. Regenerated via
  `dotnet ef dbcontext optimize --output-dir CompiledModels --namespace
  StockAndFlow.Data.CompiledModels` (run in `StockAndFlow.Core`). **Rule: regenerate the
  compiled model on every entity/property change before releasing.**
- Also fixed: release branch's WPF + test projects were on EF 9 packages while Core was on
  EF 10 (solution didn't build; invisible because CI only built Mobile).

### 4.16 "Your cost" auto-calculates from the BOM (2026-08-01, shipped in v1.1.4)
- With a BOM attached, **Your cost = materials + extras** and the field becomes read-only:
  - **Materials cost** = Σ(component cost × amount used), recalculated live as components are
    added/removed and refreshed from current component prices when the editor opens.
  - **"Extra costs per item (labor, packaging)"** — new user-owned field, persisted as
    `InventoryItems.ExtraCostPerUnit` (auto-migrated, default 0). The app never modifies it,
    so price refreshes can't clobber a labor markup (the "annoying" problem, solved
    structurally by giving each number its own field).
- BOM box shows the breakdown (materials / extras / total) and each component row shows its
  amount with units + cost contribution ("Wax × 2.5 oz  $0.75").
- Merge to the release branch hit a real conflict in `AddEditInventoryPage.xaml` — onboarding's
  version uses BindableLayout (CollectionView-in-ScrollView crash fix) + dark-mode-safe colors;
  resolved by keeping that structure and weaving the new UI in (with `AppThemeBinding` on the
  new caption).

### 4.17 Releases v1.1.3 + v1.1.4 (2026-08-01)
- Both released via the flow: commit on `maui-migration` → merge into `worktree-onboarding` →
  regenerate compiled model (when the EF model changed) → bump `ApplicationDisplayVersion` →
  tag `vX.Y.Z` → push tag (triggers both platform builds).
- **v1.1.3**: friendlier labels + help icons, measure-by-weight, BOM fixes, compiled-model fix,
  test suite + tests CI. **v1.1.4**: BOM cost auto-calc + extra-costs field.
- Both times: Android → Google Play Internal Testing (automatic), iOS → TestFlight (automatic
  upload; Apple processing ~10–30 min). CI runs monitored via the public GitHub API
  (`gh` CLI now installed for next time).

### 4.18 Shopify sync: fixed, given a UI, and live-verified (2026-08-02, commit `21d66af`)
**The feature had never worked.** Four defects, none of which would ever surface as an error:
1. DTOs parsed Shopify's snake_case JSON with .NET defaults (PascalCase, strict) → every
   response deserialized into empty objects → sync reported **"completed successfully" while
   syncing nothing**.
2. **No configuration UI existed on any platform** — the WPF "not configured" alert pointed at
   Business Settings fields that don't exist. Credentials could not be entered at all.
3. API version pinned to `2024-01`, sunset by Shopify in early 2025; no pagination (50-product
   ceiling).
4. Imported orders never got `ShopifyOrderId` stamped on the sale — the dedup key — so every
   sync would have **re-imported all orders as duplicate sales and re-deducted inventory**.

Fixes: explicit `JsonPropertyName` mappings + numbers-from-strings; nullable line-item
product/variant ids (null for custom items); API version `2026-01` as a documented const;
Link-header pagination at 250/page; order-id stamping; connection test now validates the body
parses as a shop (wrong store names return HTML 200s); per-request auth headers instead of
mutating a static `HttpClient.BaseAddress` (throws after first use — credentials could never be
changed at runtime); store-name normalization (`https://x.myshopify.com/admin` → `x`);
injectable `HttpMessageHandler` for tests. **13 new tests** against real-shape Shopify JSON.

New **Shopify Sync** settings screen on both heads (shared `ShopifySettingsViewModel`):
enable toggle, store name + token with help text, Test Connection, Sync Now, live status.

**Live verification (2026-08-02)** — Shopify Partners → Dev Dashboard:
- Dev store **`stockandflow-test.myshopify.com`** (Basic plan, sample data), org NewSpark.dev.
- Custom app **"Stock & Flow"**, installed, scopes `read_products`, `read_orders`,
  `read_inventory`, `write_inventory`. Admin API token is single-reveal — held by the user only.
- ✅ Test Connection → connected. ✅ Sync Now → **real products appear in Inventory**.
- ✅ Bonus proof of the credential work: the token is on disk as `enc1:`-prefixed DPAPI
  ciphertext in `Data/settings.json` — the hardening from `60a9dfb` working on a real secret.
- ⚠️ **Order→sale sync not yet live-verified** — the dev store's sample data contains no orders.
  Covered by unit tests (incl. dedup + single deduction); to close the gap, place a test order
  in the dev store admin and re-sync.
- Gotcha for next time: the WPF app keeps its SQLite data in an uncheckpointed WAL while running,
  so the DB can't be inspected externally until the app exits cleanly — verify through the UI.

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
7. **Stale EF compiled model truncated fractional stock (90.5 → 90) and lacked BomComponent**
   — `UseModel` bypasses `OnModelCreating`; caught by the new migration tests, fixed by
   regenerating the model (see §4.15).
8. **BOM deductions rounded to whole units** — 2.5 oz per candle deducted as 3 oz; fixed by the
   decimal-quantities conversion (§4.14).
9. **BOM deductions silently skipped when a component ran short** (stock overstated with no
   warning) — components now go negative instead; sale edits also cascade to components (§4.14).
10. **`Data/` gitignore rule silently excluded a test folder** — migration tests never made it
    into a commit until a branch-to-branch test-count mismatch exposed it; tests moved to
    `StockAndFlow.Tests/Unit/Migrations/`.
11. **Release branch didn't compile outside Mobile** — WPF/tests on EF 9 packages vs Core on
    EF 10; invisible because CI only built the mobile app (now guarded by the tests workflow).
12. **Pre-customer databases crashed on upgrade** ("no such column: s.CustomerId", every sales
    query failed) — `DatabaseMigrationHelper` never handled the customer feature, and the
    migration tests' "legacy" fixture already included `CustomerId`, masking the gap. Found
    2026-08-02 by running a genuine old database on the emulator during credential verification;
    fixed in `95f9e89` (migrate `Sales.CustomerId` + `Customers` table; fixture corrected to the
    true original schema; emulator-verified).

---

## 6. Open items / what's NOT done yet

### CI / store delivery
- **v1.1.5** tagged Aug 2, 2026 (WPF parity + credential hardening + pre-customer DB migration
  fix) — **milestone: last planned engineering release before monetization work**. Android →
  Google Play internal, iOS → TestFlight via tag CI.
- **v1.1.4** live in both channels (Aug 1, 2026): Google Play internal testing + TestFlight.
- Unit tests run on every push via `.github/workflows/tests.yml`.
- Store listings not yet submitted: screenshots, descriptions, feature graphic, content rating, privacy policy.

### Follow-ups queued by recent work
- If testers override cost expectations differently, consider surfacing negative component
  stock more loudly (it currently just shows red/negative in inventory).
- ~~WPF desktop doesn't expose the unit-of-measure picker or extra-costs field~~ ✅ Fixed
  2026-08-02 (commit `30bd0db`): Add/Edit Inventory dialog now has the measure picker,
  unit-aware labels with help tooltips, read-only auto-calculated cost when a BOM exists,
  and the materials/extras cost breakdown in the BOM box. On `maui-migration`, not yet
  merged to the release branch.
- **Before every release: regenerate the EF compiled model if any entity/property changed**
  (`dotnet ef dbcontext optimize` in `StockAndFlow.Core` on `worktree-onboarding`).

### Needs user resources / accounts
- Real-hardware smoke test (iPhone/iPad) — TestFlight testers doing this now.
- On-device verification of: Excel import round-trip; iOS invoice render; barcode scan on real device.
- ~~Security review of mobile credential encryption~~ ✅ Done 2026-08-02 (commit `60a9dfb`).
  Findings fixed: fire-and-forget key init could silently leave the plaintext passthrough active
  (now awaited, key regenerated on SecureStorage failure, session-only key as last resort — never
  plaintext); `MigrateToEncrypted` now runs on mobile too and upgrades legacy ciphertext without
  double-encrypting; ciphertext carries an `enc1:` marker so IsProtected no longer guesses from
  base64 shape (also fixes a WPF settings-load crash on base64-looking plain text); mobile moved
  from AES-CBC to authenticated AES-GCM; Android backup now excludes `settings.json` + shared
  prefs. 9 new unit tests (81 total). On `maui-migration`, not yet merged to the release branch.
  **Emulator-verified 2026-08-02**: planted plaintext creds migrate to `enc1:` on launch, second
  launch is byte-identical (idempotent), and deleting SecureStorage (backup-restore simulation)
  regenerates the key without crashing. Note: debug deploys need `dotnet build -t:Install`, not
  `adb install` (fast deployment leaves stale assemblies).
- Google Play and App Store public release submissions (after tester sign-off).

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
- **Releases from `worktree-onboarding`**, features on `maui-migration` — the release branch
  carries CI, version, tags, and the EF compiled model; merge before tagging.
- **EF compiled model** (iOS AOT fix) must be **regenerated on every model change** — it
  silently overrides `OnModelCreating`, and a stale one truncated decimal stock (§4.15).
- **BOM stock follows recorded sales unconditionally** — negative component stock is honest and
  self-correcting; silently skipping deductions overstates inventory.
- **Cost = materials + user-owned extras** (two fields, not one) — structural fix so
  recalculating material prices can never overwrite a manually entered labor/packaging cost.

---

## 9. Reference docs in the repo
- `MONETIZATION_PLAN.md` — **locked 2026-08-02**: free tier (30 items, unlimited sales +
  export, 5 invoices/mo), Pro $8.99/mo · $49.99/yr · $99.99 founding lifetime, testers get
  lifetime Pro, RevenueCat + sync-code licensing (no accounts/backend). Next build phase.
- `MOBILE_MIGRATION_PLAN.md` — the migration plan + what's portable.
- `RELEASE_CHECKLIST.md` — store-release checklist with status.
- `BUSINESS_PLAN.md` — product/business context.
- `build-release.cmd` / `build-release.sh` — signed build scripts.
- `README.md` — project overview (tech stack updated: SkiaSharp for PDF).
