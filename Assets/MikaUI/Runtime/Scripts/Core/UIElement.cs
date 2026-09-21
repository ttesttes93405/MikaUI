
using System;

namespace MikaUI
{
    public sealed record UIElement<TContainer>
    {
        public string UIName { get; init; }
        public Func<IVisualUI> GetTemplate { get; init; }

        public Func<TContainer, MikaTask<(IVisualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVisualUI> Recovery { get; init; }

    }

    public sealed record VirtualUIElement
    {
        public string UIName { get; init; }

        public Func<MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVirtualUI> Recovery { get; init; }

    }
}