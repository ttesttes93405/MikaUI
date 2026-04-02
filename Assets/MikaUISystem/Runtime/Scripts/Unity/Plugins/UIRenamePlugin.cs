using System;
using System.Collections.Generic;
using MikaUISystem.Plugin;
using UnityEngine;

namespace MikaUISystem
{
    public sealed class UIRenamePlugin :
        IPlugin<MonoBehaviour, Transform>,
        IPluginUICreatedHandler<MonoBehaviour, Transform>,
        IPluginUIRecoveryedHandler<MonoBehaviour>
    {
        readonly Dictionary<(Type, string), string> nameMap = new();

        public int SortingOrder => 2;

        public void Install(UIManager<MonoBehaviour, Transform> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T> token, Transform container, IUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IUI
        {
            var basename = string.IsNullOrEmpty(name) ? template.name : name;
            nameMap[(typeof(T), name)] = basename;
            token.UI.name = $"{basename}";
        }


        public void OnUIRecoveryed<T>(string name, UIControlToken<T> token) where T : MonoBehaviour, IUI
        {
            var basename = nameMap[(typeof(T), name)];
            token.UI.name = $"[Recovery] {basename}";
        }


    }


}