
using System;

namespace MikaUISystem
{

    public delegate MikaTask<(T ui, Action onCreated, Guid elementID)> UIElementCreatedHandler<T>();

    public sealed record UIElement<TContainer>
    {
        public string UIName { get; init; }
        public Func<IUI> GetTemplate { get; init; }

        public Func<TContainer, MikaTask<(IUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Func<TContainer, IUI> Unique { get; init; }
        public Func<IUI, MikaTask> Recovery { get; init; }

    }

    public sealed record VirtualUIElement
    {
        public string UIName { get; init; }

        public Func<MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>> Create { get; init; }
        public Action<IVirtualUI> Recovery { get; init; }

    }
}