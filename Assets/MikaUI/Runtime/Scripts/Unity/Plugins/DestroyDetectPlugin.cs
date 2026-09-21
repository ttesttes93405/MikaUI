using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace MikaUI.Plugin
{
    sealed class UnexpectedDestructionRunner : MonoBehaviour { }

    public class DestroyDetectPlugin :
        IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUIWillRecoveryHandler<MonoBehaviour, Transform>,
        IPluginUIUnexpectedDestroyedHandler
    {
        public int SortingOrder => 1;

        UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager;
        int managerHashCode;
        readonly Action<UIControlToken> onUIDestroyAction;
        readonly Dictionary<Guid, Action> unsubscribeDetects = new();
        readonly Queue<UIControlToken> unexpectedDestructionTokens = new();
        readonly HashSet<Guid> queuedTokenIds = new();
        UnexpectedDestructionRunner runner;
        Coroutine processing;
        bool isUninstalled;

        public DestroyDetectPlugin(Action<UIControlToken> onUIDestroyAction)
        {
            this.onUIDestroyAction = onUIDestroyAction;
        }

        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            managerHashCode = manager.GetHashCode();
            isUninstalled = false;

            var runnerObject = new GameObject("[MikaUI] Destroy Detection Runner");
            runnerObject.transform.SetParent(manager.RootContainer, false);
            runner = runnerObject.AddComponent<UnexpectedDestructionRunner>();
        }

        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            foreach (var unsubscribe in unsubscribeDetects.Values)
            {
                unsubscribe?.Invoke();
            }
            unsubscribeDetects.Clear();

            isUninstalled = true;
            unexpectedDestructionTokens.Clear();
            queuedTokenIds.Clear();
            if (runner != null)
            {
                runner.StopAllCoroutines();
                UnityEngine.Object.Destroy(runner.gameObject);
                runner = null;
            }
            processing = null;
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
                            EnqueueUnexpectedDestruction(token);
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

        void EnqueueUnexpectedDestruction(UIControlToken token)
        {
            if (isUninstalled || token == null || token.IsDisposed || queuedTokenIds.Add(token.TokenID) == false)
            {
                return;
            }

            unexpectedDestructionTokens.Enqueue(token);
            processing ??= runner.StartCoroutine(ProcessUnexpectedDestructions());
        }

        IEnumerator ProcessUnexpectedDestructions()
        {
            while (isUninstalled == false && unexpectedDestructionTokens.Count > 0)
            {
                // OnDestroy runs before Unity has completed its destruction cycle.
                // Fake-null is only reliable on the following frame.
                yield return null;

                var count = unexpectedDestructionTokens.Count;
                for (var i = 0; i < count; i++)
                {
                    var token = unexpectedDestructionTokens.Dequeue();
                    queuedTokenIds.Remove(token.TokenID);

                    if (isUninstalled == false && manager?.IsDisposed == false && token.IsDisposed == false)
                    {
                        try
                        {
                            manager.HandleUnexpectedDestruction(token);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }

            processing = null;
        }
    }
}
