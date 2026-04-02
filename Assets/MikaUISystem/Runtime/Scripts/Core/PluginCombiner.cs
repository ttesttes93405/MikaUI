
using System.Collections.Generic;
using System.Linq;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    internal class PluginCombiner<TUI, TContainer> :
        IPlugin<TUI, TContainer>,
        IPluginUICreatedHandler<TUI, TContainer>,
        IPluginUIWillRecoveryHandler<TUI>,
        IPluginUIRecoveryedHandler<TUI>,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class
    {


        readonly IPlugin<TUI, TContainer>[] plugins;
        readonly IPlugin<TUI, TContainer>[] reversePlugins;

        public int SortingOrder => throw new System.NotImplementedException();

        public PluginCombiner(IEnumerable<IPlugin<TUI, TContainer>> plugins)
        {
            this.plugins = plugins
                .OrderBy(p => p.SortingOrder)
                .ToArray();
                
            reversePlugins = plugins.Reverse().ToArray();
        }


        public void Install(UIManager<TUI, TContainer> manager)
        {
            foreach (var plugin in plugins)
            {
                plugin.Install(manager);
            }
        }

        public void OnUICreated<T>(string name, UIControlToken<T> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : IUI, TUI
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginUICreatedHandler<TUI, TContainer> pluginUICreatedHandler)
                    pluginUICreatedHandler.OnUICreated(name, token, container, parentUI, slotRectConfigs, template);
            }
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T> token) where T : IUI, TUI
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIWillRecoveryHandler<TUI> pluginUIWillRecoveryHandler)
                    pluginUIWillRecoveryHandler.OnUIWillRecovery(name, token);
            }
        }

        public void OnUIRecoveryed<T>(string name, UIControlToken<T> token) where T : IUI, TUI
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIRecoveryedHandler<TUI> pluginUIRecoveryedHandler)
                    pluginUIRecoveryedHandler.OnUIRecoveryed(name, token);
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