using System;
using UnityEngine;
using System.Collections.Generic;

namespace MikaUI.Plugin
{
    public class DestroyDetectPlugin :
        IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUIWillRecoveryHandler<MonoBehaviour, Transform>,
        IPluginUIUnexpectedDestroyedHandler
    {
        public int SortingOrder => 1;

        int managerHashCode;
        UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager;
        readonly Action<UIControlToken> onUIDestroyAction;
        readonly Dictionary<Guid, Action> unsubscribeDetects = new();

        public DestroyDetectPlugin(Action<UIControlToken> onUIDestroyAction)
        {
            this.onUIDestroyAction = onUIDestroyAction;
        }

        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            this.manager = manager;
            managerHashCode = manager.GetHashCode();
        }

        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            foreach (var unsubscribe in unsubscribeDetects.Values)
            {
                unsubscribe?.Invoke();
            }
            unsubscribeDetects.Clear();
            this.manager = null;
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
        {
            unsubscribeDetects.Add(
                token.TokenID,
                GameObjectDestroyListener.AttachTo(
                    managerHashCode,
                    token.UI.gameObject,
                    () =>
                    {
                        try
                        {
                            onUIDestroyAction?.Invoke(token);
                        }
                        finally
                        {
                            manager?.HandleUnexpectedDestruction(token);
                        }
                    })
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

        public void OnUIUnexpectedDestroyed(string name, Guid tokenID)
        {
            if (unsubscribeDetects.TryGetValue(tokenID, out var unsubscribe))
            {
                // OnDestroy may be enumerating this listener's callbacks. The
                // listener snapshots them, so removal is safe even in that case.
                unsubscribe?.Invoke();
                unsubscribeDetects.Remove(tokenID);
            }
        }
    }
}
