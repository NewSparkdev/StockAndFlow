using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Pages;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// Shows the paywall as a modal page and waits for it to close, so a gate can re-check
/// entitlement once the user is done with it.
/// </summary>
public sealed class MauiPaywallPresenter : IPaywallPresenter
{
	private readonly IServiceProvider _services;

	public MauiPaywallPresenter(IServiceProvider services) => _services = services;

	public async Task ShowPaywallAsync(ProFeature? triggeredBy = null)
	{
		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			var navigation = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
			if (navigation == null)
				return;

			var vm = new PaywallViewModel(
				_services.GetRequiredService<EntitlementService>(),
				_services.GetRequiredService<IDialogService>(),
				_services.GetRequiredService<IPurchaseService>(),
				triggeredBy);

			// Awaited via the close event so the caller can act on the outcome (e.g. carry on
			// into the editor if they upgraded) instead of firing and forgetting.
			var closed = new TaskCompletionSource();
			vm.CloseRequested += (_, _) => closed.TrySetResult();

			var page = new PaywallPage(vm);
			page.Disappearing += (_, _) => closed.TrySetResult();

			await navigation.PushModalAsync(new NavigationPage(page));
			await closed.Task;
		});
	}
}
