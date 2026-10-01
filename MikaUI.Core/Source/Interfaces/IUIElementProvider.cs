using System;

namespace MikaUI
{
    public interface IUIElementProvider<TUI, TContainer> : IDisposable where TContainer : class
    {
        public MikaTask<UIElement<TContainer>> GetUIElement<T>(string name) where T : TUI, IVisualUI;

        public VirtualUIElement GetVirtualUIElement<T>() where T : class, IVirtualUI, new();

    }
}
