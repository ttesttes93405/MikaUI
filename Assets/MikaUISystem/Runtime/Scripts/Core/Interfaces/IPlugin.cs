
namespace MikaUISystem.Plugin
{
    public interface IPlugin<TUI, TContainer> where TUI : class where TContainer : class
    {
        public int SortingOrder { get; }

        public void Install(UIManager<TUI, TContainer> manager);
    }

    public interface IPluginUICreatedHandler<TUI, TContainer> where TUI : class where TContainer : class
    {
        public void OnUICreated<T>(string name, UILifeToken<T> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : TUI, IUI;
    }

    public interface IPluginUIWillRecoveryHandler<TUI> where TUI : class
    {
        public void OnUIWillRecovery<T>(string name, UILifeToken<T> token) where T : TUI, IUI;
    }

    public interface IPluginUIRecoveryedHandler<TUI> where TUI : class
    {
        public void OnUIRecoveryed<T>(string name, UILifeToken<T> token) where T : TUI, IUI;
    }

    public interface IPluginVirtualUICreatedHandler
    {
        public void OnVirtualUICreated<T>(VirtualUILifeToken<T> token, IUI parentUI) where T : IVirtualUI, new();
    }
}