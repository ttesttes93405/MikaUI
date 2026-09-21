
using System;
using System.Collections.Generic;
using System.Linq;
using MikaUI.Plugin;

namespace MikaUI
{
    internal class PluginCombiner<TUI, TContainer, TSlotConfig> :
        IPlugin<TUI, TContainer, TSlotConfig>,
        IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>,
        IPluginUIWillRecoveryHandler<TUI, TContainer>,
        IPluginUIRecoveredHandler,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class where TSlotConfig : class
    {


        readonly IPlugin<TUI, TContainer, TSlotConfig>[] plugins;
        readonly IPlugin<TUI, TContainer, TSlotConfig>[] reversePlugins;

        public int SortingOrder => 0;

        public PluginCombiner(IEnumerable<IPlugin<TUI, TContainer, TSlotConfig>> plugins)
        {
            var orderedPlugins = (plugins ?? Enumerable.Empty<IPlugin<TUI, TContainer, TSlotConfig>>())
                .OrderBy(p => p.SortingOrder)
                .ToArray();

            this.plugins = orderedPlugins;
            reversePlugins = orderedPlugins.Reverse().ToArray();
        }


        public void Install(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            foreach (var plugin in plugins)
            {
                plugin.Install(manager);
            }
        }

        public void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            foreach (var plugin in reversePlugins)
            {
                plugin.Uninstall(manager);
            }
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs, TUI template) where T : IVisualUI, TUI
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginUICreatedHandler<TUI, TContainer, TSlotConfig> pluginUICreatedHandler)
                    pluginUICreatedHandler.OnUICreated(name, token, container, parentUI, slotRectConfigs, template);
            }
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, TContainer> token) where T : IVisualUI, TUI
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIWillRecoveryHandler<TUI, TContainer> pluginUIWillRecoveryHandler)
                    pluginUIWillRecoveryHandler.OnUIWillRecovery(name, token);
            }
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIRecoveredHandler pluginUIRecoveredHandler)
                    pluginUIRecoveredHandler.OnUIRecovered(name, tokenID);
            }
        }


        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : IVirtualUI, new()
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginVirtualUICreatedHandler pluginVirtualUICreatedHandler)
                    pluginVirtualUICreatedHandler.OnVirtualUICreated(token, parentUI);
            }
        }

    }
}
