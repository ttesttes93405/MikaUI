using System;
using System.Collections.Generic;
using MikaUISystem.Plugin;
using UnityEngine;

namespace MikaUISystem
{
    public sealed class UIRenamePlugin :
        IPlugin<MonoBehaviour, Transform>,
        IPluginUICreatedHandler<MonoBehaviour, Transform>,
        IPluginUIWillRecoveryHandler<MonoBehaviour, Transform>,
        IPluginUIRecoveryedHandler
    {
        readonly Dictionary<Guid, string> nameMap = new();

        public int SortingOrder => 2;

        public void Install(UIManager<MonoBehaviour, Transform> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IUI
        {
            var basename = string.IsNullOrEmpty(name) ? template.name : name;
            nameMap[token.TokenID] = basename;

            if (token.UI == null)
                return;

            token.UI.name = $"{basename}";
        }


        public void OnUIWillRecovery<T>(string name, UIControlToken<T, Transform> token) where T : MonoBehaviour, IUI
        {
            if (nameMap.TryGetValue(token.TokenID, out var basename) == false)
                return;

            if (token.UI == null)
                return;

            token.UI.name = $"[Recovery] {basename}";
        }

        public void OnUIRecoveryed(string name, Guid tokenID)
        {
            nameMap.Remove(tokenID);
        }


    }


}
