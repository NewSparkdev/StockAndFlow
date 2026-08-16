using StockAndFlow.Mobile.Pages;
#if ANDROID || IOS
using Maui.RevenueCat.InAppBilling.Enums;
using Maui.RevenueCat.InAppBilling.Models;
using Maui.RevenueCat.InAppBilling.Services;
#endif

namespace StockAndFlow.Mobile.Services;

/// <summary>
/// Tracks whether the user owns Stock &amp; Flow Pro (subscription or lifetime unlock) and
/// wraps all store-billing calls. Free-tier rules come from MONETIZATION_PLAN.md (locked
/// 2026-08-02): a 30-item setup cap plus monthly-reset invoice/import allowances — sales
/// recording and Excel export are never gated.
///
/// Billing backend: RevenueCat on both Android and iOS (the plan's licensing architecture).
///  - iOS: server-side receipt validation gives exact subscription expiry, which the raw
///    StoreKit receipt path cannot (a cancelled sub/trial would stay Pro).
///  - Android: RevenueCat's SDK carries Google Play Billing Library 8+, required by Play
///    for all updates from 2026-08-30 (Plugin.InAppBilling capped out at Billing v7 and
///    was removed).
///  - Windows: desktop edition, licensed separately — never gated.
///
/// The entitlement is cached in SecureStorage so Pro keeps working offline; RefreshAsync
/// re-verifies against the store when a connection is available and only revokes Pro on a
/// definitive "no active purchases" answer, never on a network error. Monthly counters live
/// in Preferences on-device (plan-accepted risk: wiping app data resets them).
/// </summary>
public sealed class EntitlementService
{
	public const string MonthlyProductId = "stockandflow.pro.monthly";
	public const string YearlyProductId = "stockandflow.pro.yearly";
	public const string LifetimeProductId = "stockandflow.pro.lifetime";

	// RevenueCat public SDK keys (public by design — safe to embed).
	private const string RevenueCatAppleApiKey = "appl_LneDRhuYQGRVHyrllMTYpswqlWO";
	private const string RevenueCatGoogleApiKey = "goog_yXYLRkkkbUeRxqZEXcbqLZFjQzi";

	// Free-tier limits (MONETIZATION_PLAN.md §2).
	public const int FreeMaxInventoryItems = 30;   // Setup-time cap, includes BOM raw materials.
	public const int FreeMaxInvoicesPerMonth = 5;  // Resets monthly.
	public const int FreeMaxImportsPerMonth = 2;   // Resets monthly.

	public const string InvoiceQuota = "invoices";
	public const string ImportQuota = "imports";

	private const string ProStorageKey = "pro_entitlement_active";
	private bool? _isProCache;

	private readonly IServiceProvider _services;

	public EntitlementService(IServiceProvider services)
	{
		_services = services;
	}

	/// <summary>
	/// Cached answer without touching SecureStorage — for synchronous call sites after
	/// RefreshAsync has run at startup.
	/// </summary>
	public bool IsProCached =>
		DeviceInfo.Platform == DevicePlatform.WinUI || (_isProCache ?? false);

	public async Task<bool> IsProAsync()
	{
		if (DeviceInfo.Platform == DevicePlatform.WinUI)
			return true;
		await EnsureLoadedAsync();
		return _isProCache == true;
	}

	/// <summary>Re-verifies the entitlement against the store. Safe to fire-and-forget at startup.</summary>
	public async Task RefreshAsync()
	{
		if (DeviceInfo.Platform == DevicePlatform.WinUI)
			return;

		await EnsureLoadedAsync();

#if ANDROID || IOS
		try
		{
			var owns = await RevenueCatHasProAsync();
			await SetProAsync(owns);
		}
		catch
		{
			// Offline or RevenueCat hiccup — keep the cached answer.
		}
#endif
	}

	/// <summary>Buys the given product. Returns true when the user ends up entitled.</summary>
	public async Task<(bool Success, string? Error)> PurchaseAsync(string productId)
	{
#if ANDROID || IOS
		try
		{
			return await RevenueCatPurchaseAsync(productId);
		}
		catch (Exception)
		{
			// Billing library missing/broken on this device must never crash the app.
			return (false, "Purchases aren't available on this device right now. Please try again later.");
		}
#else
		await Task.CompletedTask;
		return (false, "Purchases aren't available on this platform.");
#endif
	}

	/// <summary>Restores previous purchases (reinstall / new device). Returns true when Pro was found.</summary>
	public async Task<(bool Success, string? Error)> RestoreAsync()
	{
#if ANDROID || IOS
		try
		{
			var rc = GetRevenueCat();
			await rc.RestoreTransactions();
			var owns = await RevenueCatHasProAsync();
			await SetProAsync(owns);
			return (owns, owns ? null : "No previous Pro purchase was found for this account.");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
#else
		await Task.CompletedTask;
		return (false, "Purchases aren't available on this platform.");
#endif
	}

	/// <summary>Store-localized display prices keyed by product id; empty on failure.</summary>
	public async Task<IReadOnlyDictionary<string, string>> GetDisplayPricesAsync()
	{
		var prices = new Dictionary<string, string>();
#if ANDROID || IOS
		try
		{
			var packages = await RevenueCatGetCurrentPackagesAsync();
			foreach (var package in packages)
			{
				var id = NormalizeProductId(package.Product?.Sku);
				var price = package.Product?.Pricing?.PriceLocalized;
				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(price))
					prices[id] = price;
			}
		}
		catch
		{
			// Paywall falls back to its static price labels.
		}
#else
		await Task.CompletedTask;
#endif
		return prices;
	}

	/// <summary>
	/// Consumes one unit of a monthly free allowance (invoices, imports). Pro users always
	/// pass without consuming. Returns false when the allowance for this month is used up.
	/// </summary>
	public async Task<bool> TryConsumeMonthlyAllowanceAsync(string feature, int monthlyLimit)
	{
		if (await IsProAsync())
			return true;

		var key = $"quota_{feature}_{DateTime.Now:yyyyMM}";
		var used = Preferences.Get(key, 0);
		if (used >= monthlyLimit)
			return false;

		Preferences.Set(key, used + 1);
		return true;
	}

	/// <summary>
	/// Returns true when the caller may proceed; otherwise shows the paywall and returns false.
	/// </summary>
	public async Task<bool> EnsureProAsync(string reason)
	{
		if (await IsProAsync())
			return true;

		await ShowPaywallAsync(reason);
		return false;
	}

	/// <summary>Shows the upsell without any entitlement check (for exhausted monthly allowances).</summary>
	public static Task ShowPaywallAsync(string reason) =>
		MainThread.InvokeOnMainThreadAsync(() =>
		{
			var nav = Shell.Current?.Navigation
				?? Application.Current?.Windows[0].Page?.Navigation;
			return nav?.PushModalAsync(new NavigationPage(new PaywallPage(reason))) ?? Task.CompletedTask;
		});

#if ANDROID || IOS
	private IRevenueCatBilling GetRevenueCat()
	{
		var rc = (IRevenueCatBilling)_services.GetService(typeof(IRevenueCatBilling))!;
		if (!rc.IsInitialized())
		{
			var apiKey = DeviceInfo.Platform == DevicePlatform.Android
				? RevenueCatGoogleApiKey
				: RevenueCatAppleApiKey;
			rc.Initialize(apiKey);
		}
		return rc;
	}

	// Play products come back as "productId:basePlanId" (e.g. "stockandflow.pro.monthly:monthly");
	// App Store products are the bare id. Strip the base plan so both stores key identically.
	private static string? NormalizeProductId(string? sku)
	{
		if (string.IsNullOrEmpty(sku))
			return sku;
		var colon = sku.IndexOf(':');
		return colon < 0 ? sku : sku[..colon];
	}

	private async Task<bool> RevenueCatHasProAsync()
	{
		var rc = GetRevenueCat();
		var info = await rc.GetCustomerInfo();
		if (info == null)
			throw new InvalidOperationException("RevenueCat returned no customer info.");

		// ActiveSubscriptions is receipt-validated by RevenueCat: expired/cancelled subs
		// are excluded. Lifetime is a non-consumable so any past purchase of it counts.
		if (info.ActiveSubscriptions?.Any(p => NormalizeProductId(p) is MonthlyProductId or YearlyProductId) == true)
			return true;

		return info.AllPurchasedIdentifiers?.Any(p => NormalizeProductId(p) == LifetimeProductId) == true;
	}

	private async Task<List<PackageDto>> RevenueCatGetCurrentPackagesAsync()
	{
		var rc = GetRevenueCat();
		var offerings = await rc.GetOfferings();
		var current = offerings?.FirstOrDefault(o => o.IsCurrent) ?? offerings?.FirstOrDefault();
		return current?.AvailablePackages ?? [];
	}

	private async Task<(bool Success, string? Error)> RevenueCatPurchaseAsync(string productId)
	{
		try
		{
			var packages = await RevenueCatGetCurrentPackagesAsync();
			var package = packages.FirstOrDefault(p => NormalizeProductId(p.Product?.Sku) == productId);
			if (package == null)
				return (false, "This product is not available right now. Please try again later.");

			var rc = GetRevenueCat();
			var result = await rc.PurchaseProduct(package);
			if (result.IsSuccess)
			{
				await SetProAsync(true);
				return (true, null);
			}

			if (result.ErrorStatus == PurchaseErrorStatus.PurchaseCancelledError)
				return (false, null);

			return (false, $"The purchase could not be completed ({result.ErrorStatus}).");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}
#endif

	private async Task EnsureLoadedAsync()
	{
		if (_isProCache != null)
			return;
		try
		{
			_isProCache = await SecureStorage.Default.GetAsync(ProStorageKey) == "1";
		}
		catch
		{
			_isProCache = false;
		}
	}

	private async Task SetProAsync(bool value)
	{
		_isProCache = value;
		try
		{
			if (value)
				await SecureStorage.Default.SetAsync(ProStorageKey, "1");
			else
				SecureStorage.Default.Remove(ProStorageKey);
		}
		catch
		{
			// SecureStorage unavailable — the in-memory cache still covers this session.
		}
	}
}
