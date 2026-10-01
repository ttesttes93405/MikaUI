
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
        IPluginUIUnexpectedDestroyedHandler,
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
            var installedPlugins = new List<IPlugin<TUI, TContainer, TSlotConfig>>(plugins.Length);
            try
            {
                foreach (var plugin in plugins)
                {
                    plugin.Install(manager);
                    installedPlugins.Add(plugin);
                }
            }
            catch
            {
                // The caller never receives a manager when construction fails.
                // Only plugins whose Install completed can be rolled back.
                for (var i = installedPlugins.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        installedPlugins[i].Uninstall(manager);
                    }
                    catch
                    {
                        // Keep cleaning up and preserve the installation failure.
                    }
                }

                throw;
            }
        }

        public void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            Uninstall(manager, onError: null);
        }

        internal void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager, Action<Exception> onError)
        {
            foreach (var plugin in reversePlugins)
            {
                try
                {
                    plugin.Uninstall(manager);
                }
                catch (Exception exception)
                {
                    try
                    {
                        onError?.Invoke(exception);
                    }
                    catch
                    {
                        // One failed error reporter must not leave later plugins installed.
                    }
                }
            }
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs, TUI template) where T : IVisualUI, TUI
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginUICreatedHandler<TUI, TContainer, TSlotConfig> pluginUICreatedHandler)
                {
                    pluginUICreatedHandler.OnUICreated(name, token, container, parentUI, slotRectConfigs, template);
                    if (token.IsDisposed)
                        break;
                }
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

        public void OnUIUnexpectedDestroyed(string name, Guid tokenID)
        {
            foreach (var plugin in reversePlugins)
            {
                if (plugin is IPluginUIUnexpectedDestroyedHandler handler)
                    handler.OnUIUnexpectedDestroyed(name, tokenID);
            }
        }


        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : class, IVirtualUI, new()
        {
            foreach (var plugin in plugins)
            {
                if (plugin is IPluginVirtualUICreatedHandler pluginVirtualUICreatedHandler)
                {
                    pluginVirtualUICreatedHandler.OnVirtualUICreated(token, parentUI);
                    if (token.IsDisposed)
                        break;
                }
            }
        }

    }
}
