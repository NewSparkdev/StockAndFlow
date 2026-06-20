# Stock & Flow — Mobile Migration Plan (WPF → .NET MAUI)

Goal: Run on **iPhone + Android** (and keep **Windows**) from one codebase, using **.NET MAUI**.
Strategy chosen: **Extract a shared `StockAndFlow.Core` class library** that both the existing WPF
app and a new MAUI app reference. No business-logic duplication; WPF keeps working throughout.

Environment confirmed: .NET 10 SDK with `android`, `ios`, `maccatalyst`, `maui-windows` workloads installed.
(iOS can compile here but needs a Mac to deploy/sign.)

---

## Architecture target

```
StockAndFlow.sln
├─ StockAndFlow.Core      (net10.0)            ← Models, Services, ViewModels, Data, Commands  [PORTABLE]
│   └─ Platform/          interfaces: IDialogService, IFilePickerService, ICredentialProtector, IPathProvider
├─ StockAndFlow          (net10.0-windows, WPF) ← existing UI; references Core; WPF impls of the interfaces
└─ StockAndFlow.Mobile   (net10.0-android; net10.0-ios; net10.0-windows MAUI) ← new UI; MAUI impls of interfaces
```

Namespaces stay `StockAndFlow.*` (namespace need not match assembly), so most files move **unchanged**.

---

## Portability audit (what actually couples to Windows)

| Concern | Where | Fix |
|---|---|---|
| `RelayCommand` uses WPF `CommandManager.RequerySuggested` | `Commands/RelayCommand.cs` | Portable `RelayCommand` with explicit `RaiseCanExecuteChanged()` |
| `MessageBox.Show` (confirm/error) | ~7 ViewModels | `IDialogService` (async `AlertAsync`/`ConfirmAsync`) |
| `OpenFileDialog` (image/file pick) | `AddEditInventoryVM`, `AddEditExpenseVM`, `BusinessSettingsVM` | `IFilePickerService` |
| DPAPI `ProtectedData` (Windows-only) | `Services/SecureCredentialService.cs`, used by `AppSettings` | Pluggable `ICredentialProtector` (WPF=DPAPI, MAUI=SecureStorage/AES). `AppSettings.cs` unchanged. |
| File paths `AppDomain.BaseDirectory`, `"Data"`, `"Images"`, `"Logs"` | `App.xaml.cs`, `LoggingService`, image copy | `IPathProvider` → MAUI `FileSystem.AppDataDirectory` |
| Charts `LiveChartsCore.SkiaSharpView.WPF` | WPF views only | MAUI uses `LiveChartsCore.SkiaSharpView.Maui` (same API) |

Already portable (no change): all Models, EF Core + SQLite data layer, `ObservableCollection`,
`ICommand`, `HttpClient` (ShopifyService), Serilog, ClosedXML.

> **Invoices:** QuestPDF was replaced with a SkiaSharp `SKDocument` renderer (`InvoiceService`).
> QuestPDF bundles its own native Skia build (`libQuestPdfSkia`) that depends on `libstdc++` and
> fails to load on Android/iOS — it is desktop/server-only. SkiaSharp's `libSkiaSharp` is present on
> every head (it renders the charts), so one renderer now serves Windows + Android + iOS.

---

## Phases

### Phase 0 — Restructure into shared library (foundation)  ← STARTING HERE
1. Create `StockAndFlow.Core.csproj` (net10.0) with the shared NuGet packages.
2. Add `Core/Platform/` abstraction interfaces.
3. Port `RelayCommand` (remove `CommandManager`).
4. Make `SecureCredentialService` use a pluggable provider (DPAPI provider lives in WPF app).
5. `git mv` Models, Services, ViewModels, Data, Commands → Core. Refactor the ~10 platform call sites onto interfaces.
6. Trim WPF `.csproj` to UI-only + `ProjectReference` to Core; add WPF implementations of the interfaces; wire DI in `App.xaml.cs`.
7. **Verify: WPF app still builds and runs.** ← Phase 0 done.

### Phase 1 — MAUI project skeleton
- `dotnet new maui` → `StockAndFlow.Mobile`, reference Core.
- MAUI implementations of the interfaces (dialogs, `FilePicker`/`MediaPicker`, `SecureStorage`, `FileSystem`).
- `MauiProgram.cs` DI mirroring `App.xaml.cs`'s `ConfigureServices`.
- App boots to an empty Shell. **Verify: Android build + boots.**

### Phase 2 — UI screens (the bulk of the work)
- Shell with tabs: Inventory, Sales, Expenses, Adjustments, Reports, Settings.
- Rebuild each WPF view as a MAUI `ContentPage` (touch-first layouts, `CollectionView` not `DataGrid`).
- Swap charts to `LiveChartsCore.SkiaSharpView.Maui`.
- Reuse existing ViewModels as-is (they're in Core).

### Phase 3 — Storage, polish, packaging
- First-run DB seeding under `FileSystem.AppDataDirectory`; optional import of existing JSON/db.
- App icons, splash, permissions (camera/photos for image picking).
- Android: `dotnet build -t:Run -f net10.0-android`. iOS: build/sign on a Mac, TestFlight.

---

## Status log
- [x] Portability audit complete
- [x] **Phase 0 complete** — `StockAndFlow.Core` extracted (net10.0, builds clean, zero WPF refs).
      WPF app re-wired onto Core via platform interfaces (`WpfDialogService`, `WpfFilePickerService`,
      `WpfPathProvider`, `WpfCredentialProtector` (DPAPI), `WpfEditorPresenter`). Full solution builds.
- [x] **Phase 1 complete** — `StockAndFlow.Mobile` MAUI project created (Android + Windows targets;
      iOS to be re-enabled on a Mac). MAUI implementations of all 5 platform interfaces
      (`MauiDialogService`, `MauiFilePickerService`, `MauiPathProvider`, `MauiCredentialProtector`
      (AES + SecureStorage), `MauiEditorPresenter` placeholder). DI in `MauiProgram.cs` mirrors WPF.
      Dashboard `MainPage` bound to the shared `MainViewModel`. **`dotnet build -f net10.0-android` succeeds.**
      Note: required `dotnet workload restore` (workloads were VS-installed); pinned SDK band via `global.json`.
- [x] **Phase 2 complete** — Android builds clean. Full mobile UI:
      - Bottom-tab navigation shell (`AppShell.cs`): Dashboard, Inventory, Sales, Expenses, Adjustments, Reports
      - List pages: Inventory (+search), Sales, Expenses, Adjustments, Reports
      - Editor pages (all wired into `MauiEditorPresenter`, no placeholders left): Add/Edit Inventory,
        Add/Edit Expense, Record Adjustment, Record Sale (cart + tax), Export/Import, Business Settings (from Dashboard toolbar)
      - Detail pages: Inventory, Sale, Expense, Adjustment
      - Charts on Reports via `LiveChartsCore.SkiaSharpView.Maui` (sales trend, top items, expenses pie, inventory pie);
        `UseSkiaSharp()` registered in `MauiProgram`
- [ ] Phase 3 — storage/polish/packaging (app icons, permissions, first-run data, real app id, iOS on Mac, store builds, device testing)

### How to run the mobile app
- Android emulator/device: `dotnet build StockAndFlow.Mobile -t:Run -f net10.0-android`
- iOS: open on a Mac, add `net10.0-ios` back to `<TargetFrameworks>`, build/sign via Xcode toolchain.

### Phase 0 notes for the MAUI side (Phase 1)
The MAUI app must provide its own implementations of the 5 Core interfaces in `StockAndFlow.Platform`:
| Interface | MAUI implementation |
|---|---|
| `IDialogService` | `Page.DisplayAlert` (via `Shell.Current` / current page) |
| `IFilePickerService` | `MediaPicker.PickPhotoAsync` / `FilePicker`, copy stream into `ImagesDirectory` |
| `IPathProvider` | `FileSystem.AppDataDirectory` for Data/Images/Logs |
| `ICredentialProtector` | AES key kept in `SecureStorage` (Keychain/Keystore) — set `SecureCredentialService.Provider` at startup |
| `IEditorPresenter` | `Navigation.PushModalAsync(editorPage)` with the editor VM as BindingContext |

DI in `MauiProgram.cs` mirrors `App.xaml.cs.ConfigureServices`. Editor VMs can be built with
`ActivatorUtilities.CreateInstance` exactly as `WpfEditorPresenter` does.
