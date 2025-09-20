using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MikaUISystem.Plugin
{
    public static class PluginCreator
    {
        public static IEnumerable<IPlugin<MonoBehaviour, Transform>> Create(params IPlugin<MonoBehaviour, Transform>[] plugins)
        {
            return Create(plugins.AsEnumerable());
        }

        public static IEnumerable<IPlugin<MonoBehaviour, Transform>> Create(IEnumerable<IPlugin<MonoBehaviour, Transform>> plugins = null)
        {
            var corePlugins = new IPlugin<MonoBehaviour, Transform>[]
            {
                new InjectUIManagerPlugin<MonoBehaviour, Transform>(),
                new UIInitPlugin<MonoBehaviour, Transform>(),         // Init should be called after InjectUIManager
                new UIRenamePlugin(),
                new FitContainerPlugin(),
            };

            if (plugins == null)
            {
                return corePlugins;
            }

            return corePlugins.Concat(plugins);
        }
    }

}