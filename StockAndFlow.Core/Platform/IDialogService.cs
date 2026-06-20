namespace StockAndFlow.Platform
{
    /// <summary>
    /// Platform-agnostic user dialogs. WPF implements with MessageBox; MAUI with Page.DisplayAlert.
    /// </summary>
    public interface IDialogService
    {
        Task ShowAlertAsync(string title, string message, string buttonText = "OK");

        Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No");
    }
}
