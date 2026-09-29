
using System;

namespace MikaUI
{
    public sealed record UIElement<TContainer>
    {
        public string UIName { get; init; }
        public Func<IVisualUI> GetTemplate { get; init; }

        public Func<TContainer, MikaTask<(IVisualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVisualUI> Recovery { get; init; }

        /// <summary>
        /// Called after unexpected destruction. Implementations must check whether
        /// this element is still alive: surviving children can follow normal recovery.
        /// </summary>
        public Action<IVisualUI> UnexpectedRecovery { get; init; }

    }

    public sealed record VirtualUIElement
    {
        public string UIName { get; init; }

        public Func<MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVirtualUI> Recovery { get; init; }

    }
}
