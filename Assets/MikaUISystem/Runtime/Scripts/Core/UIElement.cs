
using System;

namespace MikaUISystem
{
    public sealed record UIElement<TContainer>
    {
        public string UIName { get; init; }
        public Func<IUI> GetTemplate { get; init; }

        public Func<TContainer, MikaTask<(IUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IUI> Recovery { get; init; }

    }

    public sealed record VirtualUIElement
    {
        public string UIName { get; init; }

        public Func<MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVirtualUI> Recovery { get; init; }

    }
}