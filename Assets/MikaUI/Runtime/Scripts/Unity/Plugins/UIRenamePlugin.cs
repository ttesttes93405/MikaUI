using System;
using System.Collections.Generic;
using UnityEngine;

namespace MikaUI.Plugin
{
    public sealed class UIRenamePlugin :
        IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUIWillRecoveryHandler<MonoBehaviour, Transform>,
        IPluginUIRecoveredHandler,
        IPluginUIUnexpectedDestroyedHandler
    {
        readonly Dictionary<Guid, string> nameMap = new();

        public int SortingOrder => 2;

        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
        }

        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            nameMap.Clear();
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
        {
            var basename = string.IsNullOrEmpty(name) ? template.name : name;
            nameMap[token.TokenID] = basename;

            if (token.UI == null)
                return;

            token.UI.name = $"{basename}";
        }


        public void OnUIWillRecovery<T>(string name, UIControlToken<T, Transform> token) where T : MonoBehaviour, IVisualUI
        {
            if (nameMap.TryGetValue(token.TokenID, out var basename) == false)
                return;

            if (token.UI == null)
                return;

            token.UI.name = $"[Recovery] {basename}";
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            nameMap.Remove(tokenID);
        }

        public void OnUIUnexpectedDestroyed(string name, Guid tokenID)
        {
            nameMap.Remove(tokenID);
        }


    }


}
