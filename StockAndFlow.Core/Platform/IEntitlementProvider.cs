using System;
using System.Threading.Tasks;

namespace StockAndFlow.Platform
{
    /// <summary>
    /// Tells Core whether this install has Pro. Implemented per platform: mobile from the
    /// store's billing records (via RevenueCat), desktop from a licence key. Core never talks
    /// to a store or a server itself, which is what keeps the app account-free.
    /// </summary>
    public interface IEntitlementProvider
    {
        /// <summary>True when Pro is active. Must be readable synchronously and offline.</summary>
        bool IsPro { get; }

        /// <summary>
        /// Re-checks entitlement with the platform (store receipt, cached licence). Should
        /// swallow network failures and leave the last known answer in place — losing signal
        /// must never silently downgrade a paying customer mid-market-day.
        /// </summary>
        Task RefreshAsync();

        /// <summary>Raised when <see cref="IsPro"/> changes, e.g. after a purchase or restore.</summary>
        event EventHandler? StatusChanged;
    }

    /// <summary>
    /// Default provider: everything is free tier. Used until a platform installs a real one, so
    /// a missing provider under-reports entitlement rather than handing out Pro by accident.
    /// </summary>
    public sealed class FreeEntitlementProvider : IEntitlementProvider
    {
        public bool IsPro => false;

        public Task RefreshAsync() => Task.CompletedTask;

        public event EventHandler? StatusChanged;

        // Never raised; declared so the interface contract holds.
        private void Unused() => StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
