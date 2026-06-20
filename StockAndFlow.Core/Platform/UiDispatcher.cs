using System;

namespace StockAndFlow.Platform
{
    /// <summary>
    /// Marshals actions onto the UI thread in a platform-agnostic way. Service events (and the
    /// CalculationService timer) can fire on background threads; ViewModels route bound-state updates
    /// through here so collection swaps / PropertyChanged happen on the UI thread.
    /// The host sets <see cref="Post"/> at startup (WPF: Dispatcher; MAUI: MainThread). Until then it
    /// runs inline.
    /// </summary>
    public static class UiDispatcher
    {
        /// <summary>Posts an action to the UI thread. Default runs inline (safe before the host wires it up).</summary>
        public static Action<Action> Post { get; set; } = static action => action();

        /// <summary>Runs <paramref name="action"/> on the UI thread.</summary>
        public static void Run(Action action) => Post(action);
    }
}
