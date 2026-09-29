using AndroidX.Core.View;
using AndroidX.Fragment.App;

namespace StockAndFlow.Mobile;

/// <summary>
/// Keeps status-bar icons white on modal pages. MAUI shows each modal page (Settings, Record Sale,
/// editors, paywall) in a DialogFragment with its own Window and switches that window to dark
/// status-bar icons in code — ignoring both the theme and the controller MainActivity sets on the
/// main window — so the clock/battery were nearly invisible on the dark-purple bar.
/// </summary>
internal static class ModalStatusBar
{
	public static void Apply()
	{
		if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not FragmentActivity activity)
			return;

		// Posted so the dialog MAUI just pushed has been shown and has a window.
		activity.Window?.DecorView.Post(() =>
		{
			foreach (var fragment in activity.SupportFragmentManager.Fragments)
			{
				if (fragment is DialogFragment { Dialog.Window: { } window })
					WindowCompat.GetInsetsController(window, window.DecorView).AppearanceLightStatusBars = false;
			}
		});
	}
}
