using System;

namespace MikaUI
{
    /// <summary>
    /// Tracks a visual instance across uses, including its ElementID and effect cleanup.
    /// UIManager owns TokenID and the UI tree recovery process.
    /// </summary>
    public interface IUIInstanceUseLifecycle<TUI> where TUI : class, IVisualUI
    {
        /// <summary>Returns the ElementID for this use of the instance.</summary>
        Guid BeginUse(TUI ui);
        void OnCreated(TUI ui);

        /// <summary>
        /// Runs the use cleanup and returns whether the instance may be reused.
        /// A cleanup failure is propagated and must result in disposal by the caller.
        /// </summary>
        bool Recover(TUI ui, bool isAlive);
    }
}
