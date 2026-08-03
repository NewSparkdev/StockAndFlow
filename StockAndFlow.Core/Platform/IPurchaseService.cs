using System.Threading.Tasks;

namespace StockAndFlow.Platform
{
    /// <summary>Result of a purchase or restore attempt.</summary>
    public sealed record PurchaseOutcome(bool Success, string? Message = null, bool Cancelled = false)
    {
        public static PurchaseOutcome Cancel { get; } = new(false, null, true);
    }

    /// <summary>
    /// Buys Pro through the platform's own store. Implemented per platform (StoreKit / Play
    /// Billing, via RevenueCat); Core never sees a card number, a password or an account —
    /// the store handles all of it, which is what keeps the app account-free.
    /// </summary>
    public interface IPurchaseService
    {
        /// <summary>
        /// False when this build can't take payment (billing not wired, store unreachable, or
        /// desktop where there is no store). The paywall shows prices either way but must not
        /// pretend a button will work.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>Starts the store's own purchase flow for a plan id.</summary>
        Task<PurchaseOutcome> PurchaseAsync(string planId);

        /// <summary>
        /// Re-applies a purchase already made on this store account — after reinstalling, or on
        /// a new device. Must always be offered: an unreachable "Restore" is how people conclude
        /// they've paid twice.
        /// </summary>
        Task<PurchaseOutcome> RestoreAsync();
    }

    /// <summary>
    /// Placeholder until store billing is wired. Reports unavailable rather than throwing, so the
    /// paywall is fully reviewable — and can never quietly grant Pro.
    /// </summary>
    public sealed class UnavailablePurchaseService : IPurchaseService
    {
        public bool IsAvailable => false;

        public Task<PurchaseOutcome> PurchaseAsync(string planId) =>
            Task.FromResult(new PurchaseOutcome(false,
                "Purchases aren't switched on in this build yet. Everything you see here is the real plan — the buttons go live once the app is in the stores."));

        public Task<PurchaseOutcome> RestoreAsync() =>
            Task.FromResult(new PurchaseOutcome(false,
                "There's nothing to restore yet — purchases aren't switched on in this build."));
    }
}
