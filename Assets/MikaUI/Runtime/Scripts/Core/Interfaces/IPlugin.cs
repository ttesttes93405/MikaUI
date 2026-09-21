
using System;

namespace MikaUI.Plugin
{
    public interface IPlugin<TUI, TContainer, TSlotConfig>
        where TUI : class
        where TContainer : class
        where TSlotConfig : class
    {
        public int SortingOrder { get; }

        public void Install(UIManager<TUI, TContainer, TSlotConfig> manager);

        public void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager);
    }

    public interface IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>
        where TUI : class
        where TContainer : class
        where TSlotConfig : class
    {
        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs, TUI template) where T : TUI, IVisualUI;
    }

    public interface IPluginUIWillRecoveryHandler<TUI, TContainer> where TUI : class where TContainer : class
    {
        public void OnUIWillRecovery<T>(string name, UIControlToken<T, TContainer> token) where T : TUI, IVisualUI;
    }

    public interface IPluginUIRecoveredHandler
    {
        public void OnUIRecovered(string name, Guid tokenID);
    }

    public interface IPluginVirtualUICreatedHandler
    {
        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : IVirtualUI, new();
    }
}
