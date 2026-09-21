
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
    public sealed record UnexpectedDestructionInfo(Guid TokenID, string NodeName);

    enum TreeEndMode
    {
        NormalRecovery,
        UnexpectedDestruction,
    }

    public class UIManager<TUI, TContainer, TSlotConfig> : IDisposable
        where TUI : class
        where TContainer : class
        where TSlotConfig : class
    {
        readonly IUIElementProvider<TUI, TContainer> uiElementProvider;

        readonly NodeManager nodeManager;
        readonly PluginCombiner<TUI, TContainer, TSlotConfig> combinedPlugin;
        readonly Logger logger;
        bool isDisposed;

        public event Action<RecoveryErrorInfo> OnRecoveryError;
        public event Action<UnexpectedDestructionInfo> OnUnexpectedDestruction;
        public TContainer RootContainer { get; }
        public bool IsDisposed => isDisposed;

        public UIManager(
            IUIElementProvider<TUI, TContainer> uiElementProvider,
            TContainer rootContainer,
            IEnumerable<IPlugin<TUI, TContainer, TSlotConfig>> plugins,
            Logger logger)
        {
            this.uiElementProvider = uiElementProvider;
            this.logger = logger;
            RootContainer = rootContainer ?? throw new ArgumentNullException(nameof(rootContainer));

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
            ThrowIfDisposed();

            UIElement<TContainer> uiElement;
            try
            {
                uiElement = await uiElementProvider.GetUIElement<T>(name);
            }
            catch (Exception e)
            {
                logger?.LogError?.Invoke(e);
                throw;
            }

            ThrowIfDisposed();

            if (uiElement == null)
            {
                throw new NullReferenceException($"Cannot find UIElement: {typeof(T).Name}");
            }

            (var uiIns, var onCreated, var elementID) = await uiElement.Create(container);

            if (uiIns is T ui == false)
            {
                throw new NullReferenceException($"Cannot create UI {typeof(T).Name}");
            }

            if (isDisposed)
            {
                RecoverUnmanagedElement(() => uiElement.Recovery(ui));
                ThrowIfDisposed();
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
                UnexpectedRecoverySelf = () =>
                {
                    try
                    {
                        nodeManager.DetachUnexpectedNode(node, ui);
                    }
                    finally
                    {
                        try
                        {
                            combinedPlugin.OnUIUnexpectedDestroyed(token.Name, token.TokenID);
                        }
                        finally
                        {
                            // Provider bookkeeping must be cleaned up even when a
                            // plugin cannot handle the unexpected-destruction event.
                            (uiElement.UnexpectedRecovery ?? uiElement.Recovery)?.Invoke(ui);
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

                ThrowIfDisposed();

                return token;
            }
            catch
            {
                // Dispose can be called re-entrantly from a creation plugin. In
                // that case it has already recovered this token and its subtree.
                if (token.IsDisposed == false)
                {
                    RollbackFailedCreation();
                }
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

        void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        void RecoverUnmanagedElement(Action recovery)
        {
            try
            {
                recovery?.Invoke();
            }
            catch (Exception e)
            {
                try
                {
                    logger?.LogError?.Invoke(e);
                }
                catch
                {
                    // Disposal must still report the original lifecycle state.
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
            ThrowIfDisposed();

            VirtualUIElement virtualUIElement;
            try
            {
                virtualUIElement = uiElementProvider.GetVirtualUIElement<T>();
            }
            catch (Exception e)
            {
                logger?.LogError?.Invoke(e);
                throw;
            }

            ThrowIfDisposed();

            (var virtualUI, var onCreated, var elementId) = await virtualUIElement.Create();

            if (virtualUI is T ui == false)
            {
                throw new Exception($"Cannot create UI {typeof(T).Name}");
            }

            if (isDisposed)
            {
                RecoverUnmanagedElement(() => virtualUIElement.Recovery(ui));
                ThrowIfDisposed();
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
                UnexpectedRecoverySelf = () =>
                {
                    // Virtual UI has no Unity object, so its normal provider
                    // cleanup remains safe even when its visual parent vanished.
                    nodeManager.DetachUnexpectedNode(node, ui);
                    virtualUIElement.Recovery(ui);
                },
                OnRecoveryCompleted = token.CompleteDispose,
            };

            var nodeAttached = false;
            try
            {
                nodeManager.AttachNode(parentUI, node, ui);
                nodeAttached = true;

                combinedPlugin.OnVirtualUICreated(token, parentUI);
                onCreated?.Invoke();

                ThrowIfDisposed();

                return token;
            }
            catch
            {
                if (token.IsDisposed == false)
                {
                    RollbackFailedVirtualCreation();
                }
                throw;
            }

            // Keep virtual creation atomic for custom providers and plugins too.
            // The caller never receives the token when an initialization hook fails,
            // so this method must remove the node and release the virtual element.
            void RollbackFailedVirtualCreation()
            {
                if (nodeAttached)
                {
                    RollbackableRun(
                        () => node.BeforeRecoverySelf(),
                        rollbackException => ReportRecoveryError(new RecoveryErrorInfo(token.TokenID, token.Name, RecoveryPhase.BeforeRecovery, rollbackException))
                    );

                    RollbackableRun(
                        () => nodeManager.DetachNode(node, ui),
                        rollbackException => ReportRecoveryError(new RecoveryErrorInfo(token.TokenID, token.Name, RecoveryPhase.AfterRecovery, rollbackException))
                    );
                }

                RollbackableRun(
                    () => virtualUIElement.Recovery(ui),
                    rollbackException => ReportRecoveryError(new RecoveryErrorInfo(token.TokenID, token.Name, RecoveryPhase.AfterRecovery, rollbackException)),
                    () => token.CompleteDispose()
                );
            }



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
                EndTree(node, TreeEndMode.NormalRecovery);
            }
            catch (Exception e)
            {
                ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.Unexpected, e));
            }
        }

        /// <summary>
        /// Converges MikaUI bookkeeping after Unity has destroyed a managed visual
        /// element outside the normal token-driven lifecycle.
        /// </summary>
        public void HandleUnexpectedDestruction(UIControlToken token)
        {
            if (isDisposed || token == null || token.IsDisposed)
            {
                return;
            }

            if (nodeManager.TryGetNode(token.TokenID, out var node) == false)
            {
                return;
            }

            EndTree(node, TreeEndMode.UnexpectedDestruction);
        }

        /// <summary>
        /// Ends a managed subtree in child-first order. Both exit paths share the
        /// traversal, token completion, and error isolation; their node finalizers
        /// differ because unexpected destruction cannot safely access a Unity object.
        /// </summary>
        void EndTree(Node root, TreeEndMode mode)
        {
            if (root.ValidateStatus(NodeStatus.Created) == false)
            {
                return;
            }

            if (mode == TreeEndMode.NormalRecovery)
            {
                TraversePostOrder(root.TokenID, BeforeRecoveryNode);
                TraversePostOrder(root.TokenID, AfterRecoveryNode);
                return;
            }

            TraversePostOrder(root.TokenID, UnexpectedDestructionNode);
        }

        void TraversePostOrder(Guid tokenID, Action<Node> visit)
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
                TraversePostOrder(childId, visit);
            }

            visit(node);
        }

        void BeforeRecoveryNode(Node node)
        {
            try
            {
                node.BeforeRecoverySelf?.Invoke();
            }
            catch (Exception e)
            {
                ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, RecoveryPhase.BeforeRecovery, e));
            }
        }

        void AfterRecoveryNode(Node node)
        {
            EndNode(node, node.AfterRecoverySelf, RecoveryPhase.AfterRecovery, notifyUnexpectedDestruction: false);
        }

        void UnexpectedDestructionNode(Node node)
        {
            EndNode(node, node.UnexpectedRecoverySelf, RecoveryPhase.Unexpected, notifyUnexpectedDestruction: true);
        }

        void EndNode(Node node, Action finalizer, RecoveryPhase phase, bool notifyUnexpectedDestruction)
        {
            try
            {
                finalizer?.Invoke();
                if (notifyUnexpectedDestruction)
                {
                    NotifyUnexpectedDestruction(node);
                }
            }
            catch (Exception e)
            {
                ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, phase, e));
            }
            finally
            {
                try
                {
                    node.OnRecoveryCompleted?.Invoke();
                }
                catch (Exception e)
                {
                    ReportRecoveryError(new RecoveryErrorInfo(node.TokenID, node.Name, phase, e));
                }
            }
        }

        void NotifyUnexpectedDestruction(Node node)
        {
            var handlers = OnUnexpectedDestruction;
            if (handlers == null)
            {
                return;
            }

            foreach (var callback in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<UnexpectedDestructionInfo>)callback).Invoke(new UnexpectedDestructionInfo(node.TokenID, node.Name));
                }
                catch (Exception e)
                {
                    logger?.LogError?.Invoke(e);
                }
            }
        }

        public virtual void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            // Set this before recovering existing roots so an in-flight Create
            // cannot attach a node after the manager has been shut down.
            isDisposed = true;

            foreach (var node in nodeManager.GetRootNodes())
            {
                UIFullTreeRecovery(node);
            }

            combinedPlugin.Uninstall(this, exception =>
            {
                try
                {
                    logger?.LogError?.Invoke(exception);
                }
                catch
                {
                    // A logger failure must not leave later plugins installed.
                }
            });

            nodeManager.Dispose();
        }
    }


}
