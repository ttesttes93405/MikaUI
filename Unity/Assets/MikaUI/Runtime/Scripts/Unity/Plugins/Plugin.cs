using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MikaUI.Plugin
{
    public static class PluginCreator
    {
        public static IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> Create(
            CanvasProvider canvasProvider,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins = null)
        {
            var corePlugins = new IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[]
            {
                new InjectUIManagerPlugin<MonoBehaviour, Transform, SlotRectConfigs>(),
                new UIInitPlugin<MonoBehaviour, Transform, SlotRectConfigs>(),         // Init should be called after InjectUIManager
                new UIRenamePlugin(),
                new FitContainerPlugin(),
                new DestroyDetectPlugin(canvasProvider, token => Debug.LogError($"[DestroyDetectPlugin] UI is destroyed before recovery. TokenID: {token.TokenID}, Name: {token.Name}")),
            };

            if (plugins == null)
            {
                return corePlugins;
            }

            return corePlugins.Concat(plugins);
        }
    }

}
