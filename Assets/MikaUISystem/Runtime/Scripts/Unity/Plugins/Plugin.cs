using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MikaUISystem.Plugin
{
    public static class PluginCreator
    {
        public static IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> Create(params IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[] plugins)
        {
            return Create(plugins.AsEnumerable());
        }

        public static IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> Create(IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins = null)
        {
            var corePlugins = new IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[]
            {
                new InjectUIManagerPlugin<MonoBehaviour, Transform, SlotRectConfigs>(),
                new UIInitPlugin<MonoBehaviour, Transform, SlotRectConfigs>(),         // Init should be called after InjectUIManager
                new UIRenamePlugin(),
                new FitContainerPlugin(),
                new DestroyDetectPlugin(token => Debug.LogError($"[DestroyDetectPlugin] UI is destroyed before recovery. TokenID: {token.TokenID}, Name: {token.Name}")),
            };

            if (plugins == null)
            {
                return corePlugins;
            }

            return corePlugins.Concat(plugins);
        }
    }

}