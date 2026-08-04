using System.Threading.Tasks;
using StockAndFlow.Services;

namespace StockAndFlow.Platform
{
    /// <summary>
    /// Shows the upgrade screen. Core decides *when* a gate is hit; the platform decides how the
    /// screen appears (a pushed page on mobile, a dialog on desktop).
    /// </summary>
    public interface IPaywallPresenter
    {
        /// <summary>
        /// Presents the paywall, optionally leading with whichever feature the user was reaching
        /// for so the pitch matches what they were trying to do.
        /// </summary>
        Task ShowPaywallAsync(ProFeature? triggeredBy = null);
    }

    /// <summary>
    /// Used when a host hasn't supplied a presenter. Does nothing, so a gate still refuses the
    /// action and explains itself — it just can't offer the upgrade screen.
    /// </summary>
    public sealed class NoPaywallPresenter : IPaywallPresenter
    {
        public Task ShowPaywallAsync(ProFeature? triggeredBy = null) => Task.CompletedTask;
    }
}
