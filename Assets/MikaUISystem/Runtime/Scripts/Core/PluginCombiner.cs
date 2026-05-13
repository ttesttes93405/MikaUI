
using System;
using System.Collections.Generic;
using System.Linq;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    internal class PluginCombiner<TUI, TContainer> :
        IPlugin<TUI, TContainer>,
        IPluginUICreatedHandler<TUI, TContainer>,
        IPluginUIWillRecoveryHandler<TUI, TContainer>,
        IPluginUIRecoveryedHandler,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class
    {


        readonly IPlugin<TUI, TContainer>[] plugins;
        readonly IPlugin<TUI, TContainer>[] reversePlugins;

        public int SortingOrder => 0;

        public PluginCombiner(IEnumerable<IPlugin<TUI, TContainer>> plugins)
        {
            var orderedPlugins = (plugins ?? Enumerable.Empty<IPlugin<TUI, TContainer>>())
                .OrderBy(p => p.SortingOrder)
                .ToArray();

            this.plugins = orderedPlugins;
            reversePlugins = orderedPlugins.Reverse().ToArray();
        }


        public void Install(UIManager<TUI, TContainer> manager)
        {
            foreach (var plugin in plugins)
            {
                plugin.Install(manager);
            }
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : IUI, TUI
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginUICreatedHandler<TUI, TContainer> pluginUICreatedHandler)
                    pluginUICreatedHandler.OnUICreated(name, token, container, parentUI, slotRectConfigs, template);
            }
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, TContainer> token) where T : IUI, TUI
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIWillRecoveryHandler<TUI, TContainer> pluginUIWillRecoveryHandler)
                    pluginUIWillRecoveryHandler.OnUIWillRecovery(name, token);
            }
        }

        public void OnUIRecoveryed(string name, Guid tokenID) 
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIRecoveryedHandler pluginUIRecoveryedHandler)
                    pluginUIRecoveryedHandler.OnUIRecoveryed(name, tokenID);
            }
        }


        public void OnVirtualUICreated<T>(VirtualUIControlToken<T> token, IUI parentUI) where T : IVirtualUI, new()
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginVirtualUICreatedHandler pluginVirtualUICreatedHandler)
                    pluginVirtualUICreatedHandler.OnVirtualUICreated(token, parentUI);
            }
        }

    }
}
