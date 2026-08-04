using Plugin.InAppBilling;
using StockAndFlow.Mobile.Pages;

namespace StockAndFlow.Mobile.Services;

/// <summary>
/// Tracks whether the user owns Stock &amp; Flow Pro (subscription or lifetime unlock) and
/// wraps all store-billing calls. Free-tier rules come from MONETIZATION_PLAN.md (locked
/// 2026-08-02): a 30-item setup cap plus monthly-reset invoice/import allowances — sales
/// recording and Excel export are never gated.
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

	// Free-tier limits (MONETIZATION_PLAN.md §2).
	public const int FreeMaxInventoryItems = 30;   // Setup-time cap, includes BOM raw materials.
	public const int FreeMaxInvoicesPerMonth = 5;  // Resets monthly.
	public const int FreeMaxImportsPerMonth = 2;   // Resets monthly.

	public const string InvoiceQuota = "invoices";
	public const string ImportQuota = "imports";

	private const string ProStorageKey = "pro_entitlement_active";
	private bool? _isProCache;

	/// <summary>
	/// Cached answer without touching SecureStorage — for synchronous call sites after
	/// RefreshAsync has run at startup. The Windows head is the desktop edition (licensed
	/// separately, no store billing), so it is never gated by the mobile paywall.
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
				catch { /* Already acknowledged, or iOS (not required). */ }

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
		if (subs?.Any(p => p.State is PurchaseState.Purchased or PurchaseState.Restored
			&& p.ProductId is MonthlyProductId or YearlyProductId) == true)
			return true;

		var iaps = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
		return iaps?.Any(p => p.State is PurchaseState.Purchased or PurchaseState.Restored
			&& p.ProductId == LifetimeProductId) == true;
	}

	private static async Task SafeDisconnectAsync(IInAppBilling billing)
	{
		try { await billing.DisconnectAsync(); }
		catch { /* Nothing useful to do. */ }
	}
}
