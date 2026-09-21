using System;
using UnityEngine;
using System.Collections.Generic;

namespace MikaUI.Plugin
{
    public class DestroyDetectPlugin :
        IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUIWillRecoveryHandler<MonoBehaviour, Transform>
    {
        public int SortingOrder => 1;

        int managerHashCode;
        readonly Action<UIControlToken> onUIDestroyAction;
        readonly Dictionary<Guid, Action> unsubscribeDetects = new();

        public DestroyDetectPlugin(Action<UIControlToken> onUIDestroyAction)
        {
            this.onUIDestroyAction = onUIDestroyAction;
        }

        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            managerHashCode = manager.GetHashCode();
        }

        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            foreach (var unsubscribe in unsubscribeDetects.Values)
            {
                unsubscribe?.Invoke();
            }
            unsubscribeDetects.Clear();
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
        {
            unsubscribeDetects.Add(
                token.TokenID,
                GameObjectDestroyListener.AttachTo(
                    managerHashCode,
                    token.UI.gameObject,
                    () => onUIDestroyAction?.Invoke(token))
            );
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, Transform> token) where T : MonoBehaviour, IVisualUI
        {
            if (unsubscribeDetects.TryGetValue(token.TokenID, out var unsubscribe))
            {
                unsubscribe?.Invoke();
                unsubscribeDetects.Remove(token.TokenID);
            }
        }
    }
}
