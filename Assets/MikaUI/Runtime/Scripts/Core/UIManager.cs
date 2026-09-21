
using System;
using System.Collections.Generic;
using MikaUI.Plugin;


namespace MikaUI
{
    public enum RecoveryPhase
    {
        BeforeRecovery,
        AfterRecovery,
        Unexpected,
    }

    public sealed record RecoveryErrorInfo(Guid TokenID, string NodeName, RecoveryPhase Phase, Exception Exception);

    public class UIManager<TUI, TContainer, TSlotConfig> : IDisposable
        where TUI : class
        where TContainer : class
        where TSlotConfig : class
    {
        readonly IUIElementProvider<TUI, TContainer> uiElementProvider;

        readonly NodeManager nodeManager;
        readonly PluginCombiner<TUI, TContainer, TSlotConfig> combinedPlugin;
        readonly Logger logger;

        public event Action<RecoveryErrorInfo> OnRecoveryError;

        public UIManager(IUIElementProvider<TUI, TContainer> uiElementProvider, IEnumerable<IPlugin<TUI, TContainer, TSlotConfig>> plugins, Logger logger)
        {
            this.uiElementProvider = uiElementProvider;
            this.logger = logger;

            nodeManager = new NodeManager(logger);

            combinedPlugin = new PluginCombiner<TUI, TContainer, TSlotConfig>(plugins);

            combinedPlugin.Install(this);
        }


        public MikaTask<UIControlToken<T, TContainer>> Create<T>(ISlot<TContainer> slot, TSlotConfig slotRectConfigs = null, string name = "") where T : class, TUI, IVisualUI
        {
            try
            {
                return InternalCreate<T>(name, slot.Container, slot.ParentUI, slotRectConfigs);
            }
            catch (Exception e)
            {
                logger?.LogError?.Invoke(e);
                throw;
            }
        }

        async MikaTask<UIControlToken<T, TContainer>> InternalCreate<T>(string name, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs) where T : class, TUI, IVisualUI
        {
            var uiElement = uiElementProvider.GetUIElement<T>(name);

            if (uiElement == null)
            {
                throw new NullReferenceException($"Cannot find UIElement: {typeof(T).Name}");
            }

            (var uiIns, var onCreated, var elementID) = await uiElement.Create(container);

            if (uiIns is T ui == false)
            {
                throw new NullReferenceException($"Cannot create UI {typeof(T).Name}");
            }

            Node node = null;
            UIControlToken<T, TContainer> token = null;
            token = new(
                tokenID: Guid.NewGuid(),
                elementID: elementID,
                name: $"{name}<{typeof(T).Name}>",
                ui: ui,
                recovery: () => UIFullTreeRecovery(node)
            );

            node = new Node(
                token.TokenID,
                token.Name
            )
            {
                BeforeRecoverySelf = () =>
                {
                    node.ChangeStatus(from: NodeStatus.Created, to: NodeStatus.BeforeRecovered);
                    combinedPlugin.OnUIWillRecovery(token.Name, token);
                },
                AfterRecoverySelf = () =>
                {
                    try
                    {
                        nodeManager.DetachNode(node, ui);
                    }
                    finally
                    {
                        try
                        {
                            combinedPlugin.OnUIRecovered(token.Name, token.TokenID);
                        }
                        finally
                        {
                            // Returning the element to its provider must not depend on
                            // detaching the node or a post-recovery plugin succeeding.
                            uiElement.Recovery(ui);
                        }
                    }
                },
                OnRecoveryCompleted = token.CompleteDispose,
            };

            var nodeAttached = false;
            try
            {
                nodeManager.AttachNode(parentUI, node, ui);
                nodeAttached = true;

                UICreated(token);

                return token;
            }
            catch
            {
                RollbackFailedCreation();
                throw;
            }


            void UICreated(UIControlToken<T, TContainer> token)
            {
                combinedPlugin.OnUICreated(name, token, container, parentUI, slotRectConfigs, uiElement.GetTemplate() as TUI);
                onCreated?.Invoke();
            }

            // A caller receives the token only after all creation hooks have succeeded.
            // Until then this method owns the element and must compensate for a failed hook.
            void RollbackFailedCreation()
            {
                if (nodeAttached)
                {
                    RollbackableRun(
                        () => node.BeforeRecoverySelf(),
                        rollbackException => ReportRollbackFailure(RecoveryPhase.BeforeRecovery, rollbackException)
                    );

                    RollbackableRun(
                        () => nodeManager.DetachNode(node, ui),
                        rollbackException => ReportRollbackFailure(RecoveryPhase.AfterRecovery, rollbackException)
                    );

                    RollbackableRun(
                        () => combinedPlugin.OnUIRecovered(token.Name, token.TokenID),
                        rollbackException => ReportRollbackFailure(RecoveryPhase.AfterRecovery, rollbackException)
                    );
                }

                RollbackableRun(
                    () => uiElement.Recovery(ui),
                    rollbackException => ReportRollbackFailure(RecoveryPhase.AfterRecovery, rollbackException),
                    () => token.CompleteDispose()
                );
            }

            void ReportRollbackFailure(RecoveryPhase phase, Exception exception)
            {
                ReportRecoveryError(new RecoveryErrorInfo(token.TokenID, token.Name, phase, exception));
            }

        }

        void RollbackableRun(Action action, Action<Exception> onError, Action onCompleted = null)
        {
            if (action == null)
                return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                try
                {
                    onError?.Invoke(ex);
                }
                catch
                {
                    // A failed error reporter must not interrupt rollback.
                }
            }
            finally
            {
                try
                {
                    onCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    try
                    {
                        onError?.Invoke(ex);
                    }
                    catch
                    {
                        // A failed error reporter must not interrupt rollback.
                    }
                }
            }
        }

        void ReportRecoveryError(RecoveryErrorInfo errorInfo)
        {
            try
            {
                logger?.LogError?.Invoke(errorInfo.Exception);
            }
            catch
            {
                // Reporting must not interrupt the remaining cleanup steps.
            }

            var handlers = OnRecoveryError;
            if (handlers == null)
            {
                return;
            }

            foreach (var callback in handlers.GetInvocationList())
            {
                try
                {
                    var handler = (Action<RecoveryErrorInfo>)callback;
                    handler(errorInfo);
                }
                catch
                {
                    // One observer must not prevent other observers or cleanup.
                }
            }
        }

        public MikaTask<UIControlToken<T>> CreateVirtual<T>(IVirtualSlot slot = null) where T : IVirtualUI, new()
        {
            return InternalCreateVirtual<T>(slot?.ParentUI);
        }

        async MikaTask<UIControlToken<T>> InternalCreateVirtual<T>(IBaseUI parentUI) where T : IVirtualUI, new()
        {
            var virtualUIElement = uiElementProvider.GetVirtualUIElement<T>();
            (var virtualUI, var onCreated, var elementId) = await virtualUIElement.Create();

            if (virtualUI is T ui == false)
            {
                throw new Exception($"Cannot create UI {typeof(T).Name}");
            }
            UIControlToken<T> token = null;
            Node node = null;
            token = new(
                tokenID: Guid.NewGuid(),
                elementID: elementId,
                name: $"<{typeof(T).Name}>",
                ui: ui,
                recovery: () => UIFullTreeRecovery(node)
            );

            node = new(
                token.TokenID,
                token.Name
            )
            {
                BeforeRecoverySelf = () =>
                {
                    node.ChangeStatus(from: NodeStatus.Created, to: NodeStatus.BeforeRecovered);
                },
                AfterRecoverySelf = () =>
                {
                    nodeManager.DetachNode(node, ui);
                    virtualUIElement.Recovery(ui);
                },
                OnRecoveryCompleted = token.CompleteDispose,
            };

            nodeManager.AttachNode(parentUI, node, ui);

            combinedPlugin.OnVirtualUICreated(token, parentUI);
            onCreated?.Invoke();

            return token;



        }

        internal void UIFullTreeRecovery(Node node)
        {
            logger?.Log?.Invoke($"Start recovery UI {node.Name} and its children.");

            if (node.ValidateStatus(NodeStatus.Created) == false)
            {
                throw new Exception($"{node} is in wrong status: {node.Status}. Expected status: {NodeStatus.Created}");
            }

            try
            {
                TreeBeforeRecovery(node.TokenID);
                TreeAfterRecovery(node.TokenID);
            }
            catch (Exception e)
            {
                ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.Unexpected, e));
            }


            void TreeBeforeRecovery(Guid tokenID)
            {
                if (tokenID == Guid.Empty)
                {
                    logger?.LogError?.Invoke($"Invalid tokenID: {tokenID}");
                    return;
                }

                if (nodeManager.TryGetNode(tokenID, out var node) == false)
                {
                    logger?.LogError?.Invoke($"Cannot find UI node for {tokenID}");
                    return;
                }

                foreach (var childId in node.Children.ToArray())
                {
                    TreeBeforeRecovery(childId);
                }

                try
                {
                    node.BeforeRecoverySelf();
                }
                catch (Exception e)
                {
                    ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.BeforeRecovery, e));
                }
            }

            void TreeAfterRecovery(Guid tokenID)
            {
                if (tokenID == Guid.Empty)
                {
                    logger?.LogError?.Invoke($"Invalid tokenID: {tokenID}");
                    return;
                }

                if (nodeManager.TryGetNode(tokenID, out var node) == false)
                {
                    logger?.LogError?.Invoke($"Cannot find UI node for {tokenID}");
                    return;
                }

                foreach (var childId in node.Children.ToArray())
                {
                    TreeAfterRecovery(childId);
                }

                try
                {
                    node.AfterRecoverySelf();
                }
                catch (Exception e)
                {
                    ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.AfterRecovery, e));
                }
                finally
                {
                    try
                    {
                        node.OnRecoveryCompleted?.Invoke();
                    }
                    catch (Exception e)
                    {
                        ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.AfterRecovery, e));
                    }
                }
            }

        }

        public virtual void Dispose()
        {
            foreach (var node in nodeManager.GetRootNodes())
            {
                UIFullTreeRecovery(node);
            }

            combinedPlugin.Uninstall(this);

            nodeManager.Dispose();
        }
    }


}
