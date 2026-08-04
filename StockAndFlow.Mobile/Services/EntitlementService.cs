using Plugin.InAppBilling;
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
/// Billing backends (per the plan's licensing architecture):
///  - iOS: RevenueCat — server-side receipt validation gives exact subscription expiry,
///    which the raw StoreKit receipt path cannot (a cancelled sub/trial would stay Pro).
///  - Android: Plugin.InAppBilling — Google Play already reports only active subs;
///    switches to RevenueCat once the Play app is configured in the RevenueCat project.
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

	// RevenueCat public SDK key for the App Store app (public by design — safe to embed).
	private const string RevenueCatAppleApiKey = "appl_LneDRhuYQGRVHyrllMTYpswqlWO";

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

	private static bool UseRevenueCat => DeviceInfo.Platform == DevicePlatform.iOS;

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
		if (UseRevenueCat)
		{
			try
			{
				var owns = await RevenueCatHasProAsync();
				await SetProAsync(owns);
			}
			catch
			{
				// Offline or RevenueCat hiccup — keep the cached answer.
			}
			return;
		}
#endif

		var billing = CrossInAppBilling.Current;
		try
		{
			if (!await billing.ConnectAsync())
				return; // Store unreachable — keep the cached answer.

			var owns = await HasActivePurchaseAsync(billing);
			await SetProAsync(owns);
		}
		catch
		{
			// Offline or store hiccup — keep the cached answer.
		}
		finally
		{
			await SafeDisconnectAsync(billing);
		}
	}

	/// <summary>Buys the given product. Returns true when the user ends up entitled.</summary>
	public async Task<(bool Success, string? Error)> PurchaseAsync(string productId)
	{
#if ANDROID || IOS
		if (UseRevenueCat)
			return await RevenueCatPurchaseAsync(productId);
#endif
		var billing = CrossInAppBilling.Current;
		try
		{
			if (!await billing.ConnectAsync())
				return (false, "The store is not reachable right now. Please try again later.");

			var itemType = productId == LifetimeProductId ? ItemType.InAppPurchase : ItemType.Subscription;
			var purchase = await billing.PurchaseAsync(productId, itemType);
			if (purchase == null)
				return (false, null); // Cancelled.

			if (purchase.State is PurchaseState.Purchased or PurchaseState.Restored)
			{
				// Google Play requires acknowledgment or the purchase auto-refunds after 3 days.
				try { await billing.FinalizePurchaseAsync([purchase.TransactionIdentifier]); }
				catch { /* Already acknowledged. */ }

				await SetProAsync(true);
				return (true, null);
			}

			return (false, "The purchase could not be completed.");
		}
		catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled)
		{
			return (false, null);
		}
		catch (InAppBillingPurchaseException ex)
		{
			return (false, ex.Message);
		}
		finally
		{
			await SafeDisconnectAsync(billing);
		}
	}

	/// <summary>Restores previous purchases (reinstall / new device). Returns true when Pro was found.</summary>
	public async Task<(bool Success, string? Error)> RestoreAsync()
	{
#if ANDROID || IOS
		if (UseRevenueCat)
		{
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
		}
#endif
		var billing = CrossInAppBilling.Current;
		try
		{
			if (!await billing.ConnectAsync())
				return (false, "The store is not reachable right now. Please try again later.");

			var owns = await HasActivePurchaseAsync(billing);
			await SetProAsync(owns);
			return (owns, owns ? null : "No previous Pro purchase was found for this account.");
		}
		catch (InAppBillingPurchaseException ex)
		{
			return (false, ex.Message);
		}
		finally
		{
			await SafeDisconnectAsync(billing);
		}
	}

	/// <summary>Store-localized display prices keyed by product id; empty on failure.</summary>
	public async Task<IReadOnlyDictionary<string, string>> GetDisplayPricesAsync()
	{
		var prices = new Dictionary<string, string>();
#if ANDROID || IOS
		if (UseRevenueCat)
		{
			try
			{
				var packages = await RevenueCatGetCurrentPackagesAsync();
				foreach (var package in packages)
				{
					var id = package.Product?.Sku;
					var price = package.Product?.Pricing?.PriceLocalized;
					if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(price))
						prices[id] = price;
				}
			}
			catch
			{
				// Paywall falls back to its static price labels.
			}
			return prices;
		}
#endif
		var billing = CrossInAppBilling.Current;
		try
		{
			if (!await billing.ConnectAsync())
				return prices;

			var subs = await billing.GetProductInfoAsync(ItemType.Subscription, [MonthlyProductId, YearlyProductId]);
			foreach (var p in subs ?? [])
				prices[p.ProductId] = p.LocalizedPrice;

			var iaps = await billing.GetProductInfoAsync(ItemType.InAppPurchase, [LifetimeProductId]);
			foreach (var p in iaps ?? [])
				prices[p.ProductId] = p.LocalizedPrice;
		}
		catch
		{
			// Paywall falls back to its static price labels.
		}
		finally
		{
			await SafeDisconnectAsync(billing);
		}
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
			rc.Initialize(RevenueCatAppleApiKey);
		return rc;
	}

	private async Task<bool> RevenueCatHasProAsync()
	{
		var rc = GetRevenueCat();
		var info = await rc.GetCustomerInfo();
		if (info == null)
			throw new InvalidOperationException("RevenueCat returned no customer info.");

		// ActiveSubscriptions is receipt-validated by RevenueCat: expired/cancelled subs
		// are excluded, which is the whole point of the swap. Lifetime is a non-consumable
		// so any past purchase of it counts.
		if (info.ActiveSubscriptions?.Any(p => p is MonthlyProductId or YearlyProductId) == true)
			return true;

		return info.AllPurchasedIdentifiers?.Contains(LifetimeProductId) == true;
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
			var package = packages.FirstOrDefault(p => p.Product?.Sku == productId);
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

	private static async Task<bool> HasActivePurchaseAsync(IInAppBilling billing)
	{
		var subs = await billing.GetPurchasesAsync(ItemType.Subscription);
		if (subs?.Any(IsActiveSubscription) == true)
			return true;

		var iaps = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
		return iaps?.Any(p => p.State is PurchaseState.Purchased or PurchaseState.Restored
			&& p.ProductId == LifetimeProductId) == true;
	}

	private static bool IsActiveSubscription(InAppBillingPurchase p)
	{
		if (p.State is not (PurchaseState.Purchased or PurchaseState.Restored)
			|| p.ProductId is not (MonthlyProductId or YearlyProductId))
			return false;

		// Google Play only reports active subscriptions, so the store's answer is trusted as-is.
		// (iOS goes through RevenueCat and never reaches this code path.)
		if (DeviceInfo.Platform != DevicePlatform.iOS)
			return true;

		var period = p.ProductId == MonthlyProductId ? TimeSpan.FromDays(45) : TimeSpan.FromDays(380);
		return DateTime.UtcNow - p.TransactionDateUtc <= period;
	}

	private static async Task SafeDisconnectAsync(IInAppBilling billing)
	{
		try { await billing.DisconnectAsync(); }
		catch { /* Nothing useful to do. */ }
	}
}
