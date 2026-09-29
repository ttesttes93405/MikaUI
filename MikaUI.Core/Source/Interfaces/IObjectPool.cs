using System;

namespace MikaUI
{
    /// <summary>
    /// Stores idle visual UI instances by source. The provider owns this pool and
    /// disposes it after all managed UI has been recovered.
    /// </summary>
    public interface IObjectPool<TSource, TUI> : IDisposable
        where TSource : class
        where TUI : class, IVisualUI
    {
        bool TryRent(TSource source, out TUI ui);

        /// <summary>Returns false when the provider must destroy the instance.</summary>
        bool TryReturn(TSource source, TUI ui);
    }
}
