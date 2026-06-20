using System.Threading.Tasks;
using StockAndFlow.Platform;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI implementation of <see cref="IDialogService"/> using Shell/Page DisplayAlert on the main thread.
/// </summary>
public sealed class MauiDialogService : IDialogService
{
    private static Page CurrentPage =>
        Shell.Current ?? Application.Current?.Windows[0].Page
        ?? throw new InvalidOperationException("No active page for dialogs.");

    public Task ShowAlertAsync(string title, string message, string buttonText = "OK")
    {
        return MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlert(title, message, buttonText));
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlert(title, message, accept, cancel));
    }
}
