using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace StockAndFlow.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// Android 15+ draws edge-to-edge, so the purple navigation bar shows behind the system
		// status bar. Force light (white) status-bar icons so the clock/battery/signal stay
		// legible on the purple instead of being dark-on-dark and appearing cut off.
		if (Window?.DecorView is Android.Views.View decorView)
		{
			var controller = WindowCompat.GetInsetsController(Window, decorView);
			controller.AppearanceLightStatusBars = false;
		}
	}
}
