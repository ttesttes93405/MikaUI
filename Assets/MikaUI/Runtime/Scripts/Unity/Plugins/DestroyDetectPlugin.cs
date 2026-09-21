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
        readonly CanvasProvider canvasProvider;
        Action unsubscribeRootContainerDestroy;
        UnexpectedDestructionRunner runner;
        Coroutine processing;
        bool isUninstalled;
        bool isRootContainerDestroyed;

        public DestroyDetectPlugin(CanvasProvider canvasProvider, Action<UIControlToken> onUIDestroyAction)
        {
            this.canvasProvider = canvasProvider ?? throw new ArgumentNullException(nameof(canvasProvider));
            this.onUIDestroyAction = onUIDestroyAction;
        }

        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            managerHashCode = manager.GetHashCode();
            isUninstalled = false;
            isRootContainerDestroyed = false;

            unsubscribeRootContainerDestroy = GameObjectDestroyListener.AttachTo(
                managerHashCode,
                manager.RootContainer.gameObject,
                OnRootContainerDestroyed);

            var runnerObject = new GameObject("[MikaUI] Destroy Detection Runner");
            runnerObject.transform.SetParent(manager.RootContainer, false);
            runner = runnerObject.AddComponent<UnexpectedDestructionRunner>();
        }

        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
            unsubscribeRootContainerDestroy?.Invoke();
            unsubscribeRootContainerDestroy = null;

            foreach (var unsubscribe in unsubscribeDetects.Values)
            {
                unsubscribe?.Invoke();
            }
            unsubscribeDetects.Clear();

            isUninstalled = true;
            isRootContainerDestroyed = false;
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

        void OnRootContainerDestroyed()
        {
            if (isUninstalled || manager == null || manager.IsDisposed)
            {
                return;
            }

            // Child OnDestroy callbacks can run after this one while the runner is
            // already inactive. This is an ownership violation, not a recoverable
            // unexpected-destruction path, so do not start its coroutine.
            isRootContainerDestroyed = true;

            throw new InvalidOperationException(
                "[MikaUI] RootContainer was destroyed before its UIManager was disposed. " +
                "Call UIManager.Dispose() before destroying the RootContainer.");
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
                            // This runs from OnDestroy, so unregister bookkeeping
                            // only; the runner deactivates an empty Canvas next frame.
                            canvasProvider?.UnregisterWithoutRecovery(token.TokenID);
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
            if (isUninstalled || isRootContainerDestroyed || token == null || token.IsDisposed || queuedTokenIds.Add(token.TokenID) == false)
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
                        finally
                        {
                            // The OnDestroy phase has completed, so hierarchy changes
                            // are safe. This also covers the last UI on a Canvas.
                            canvasProvider?.RecoverUnusedCanvases();
                        }
                    }
                }
            }

            processing = null;
        }
    }
}
