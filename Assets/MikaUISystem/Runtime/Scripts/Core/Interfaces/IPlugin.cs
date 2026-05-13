
using System;

namespace MikaUISystem.Plugin
{
    public interface IPlugin<TUI, TContainer> where TUI : class where TContainer : class
    {
        public int SortingOrder { get; }

        public void Install(UIManager<TUI, TContainer> manager);
    }

    public interface IPluginUICreatedHandler<TUI, TContainer> where TUI : class where TContainer : class
    {
        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : TUI, IUI;
    }

    public interface IPluginUIWillRecoveryHandler<TUI, TContainer> where TUI : class where TContainer : class
    {
        public void OnUIWillRecovery<T>(string name, UIControlToken<T, TContainer> token) where T : TUI, IUI;
    }

    public interface IPluginUIRecoveryedHandler
    {
        public void OnUIRecoveryed(string name, Guid tokenID);
    }

    public interface IPluginVirtualUICreatedHandler
    {
        public void OnVirtualUICreated<T>(VirtualUIControlToken<T> token, IUI parentUI) where T : IVirtualUI, new();
    }
}