namespace MikaUI
{
    public interface IUIElementProvider<TUI, TContainer> where TContainer : class
    {
        public UIElement<TContainer> GetUIElement<T>(string name) where T : TUI, IVisualUI;

        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new();

    }
}