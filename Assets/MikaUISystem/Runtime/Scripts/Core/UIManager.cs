
using System;
using System.Collections.Generic;
using System.Linq;
using MikaUISystem.Plugin;


namespace MikaUISystem
{
    public record SlotRectConfigs
    {
        public Float2 AnchorMin { get; init; }
        public Float2 AnchorMax { get; init; }
        public Float2 AnchorPosition { get; init; }
        public Float2 Pivot { get; init; }
        public Float2 SizeDelta { get; init; }

        public SlotRectConfigs() { }

        public static SlotRectConfigs FromStretchFill(RectOffset rectOffset)
        {
            return new()
            {
                AnchorMin = new Float2(0, 0),
                AnchorMax = new Float2(1, 1),
                Pivot = new Float2(0.5f, 0.5f),
                SizeDelta = new Float2(-rectOffset.Left - rectOffset.Right, -rectOffset.Top - rectOffset.Bottom),
                AnchorPosition = new Float2((rectOffset.Left - rectOffset.Right) / 2, (rectOffset.Bottom - rectOffset.Top) / 2),
            };
        }
    }

    public class UIManager<TUI, TContainer> : IAsyncDisposable where TUI : class where TContainer : class
    {
        readonly IUIElementProvider<TUI, TContainer> uiElementProvider;

        readonly NodeManager nodeManager;
        readonly PluginCombiner<TUI, TContainer> combinedPlugin;
        readonly Logger logger;
        public UIManager(IUIElementProvider<TUI, TContainer> uiElementProvider, IEnumerable<IPlugin<TUI, TContainer>> plugins, Logger logger)
        {
            this.uiElementProvider = uiElementProvider;
            this.logger = logger;

            nodeManager = new NodeManager(logger);

            combinedPlugin = new PluginCombiner<TUI, TContainer>(plugins);

            combinedPlugin.Install(this);
        }


        public MikaTask<UIControlToken<T, TContainer>> Create<T>(ISlot<TContainer> slot, SlotRectConfigs slotRectConfigs = null, string name = "") where T : class, TUI, IUI
        {
            try
            {
                return InternalCreate<T>(name, slot.Container, slot.ParentUI,  slotRectConfigs);
            }
            catch (Exception e)
            {
                logger?.LogError?.Invoke(e);
                throw e;
            }
        }

        async MikaTask<UIControlToken<T, TContainer>> InternalCreate<T>(string name, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs) where T : class, TUI, IUI
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
            token = new()
            {
                TokenID = Guid.NewGuid(),
                ElementID = elementID,
                UI = ui,
                Name = $"{typeof(T).Name}_{name}",
                Recovery = () => UIFullTreeRecovery(node),
            };

            node = new Node(
                token.TokenID,
                token.Name
            )
            {
                BeforeRecoverySelf = () =>
                {
                    node.ChangeStatus(from: NodeStatus.Created, to: NodeStatus.BeforeRecoveryed);
                    combinedPlugin.OnUIWillRecovery(token.Name, token);
                },
                AfterRecoverySelf = () =>
                {
                    nodeManager.DetachNode(node, ui);
                    combinedPlugin.OnUIRecoveryed(token.Name, token.TokenID);
                    uiElement.Recovery(ui);
                },
            };

            nodeManager.AttachNode(parentUI, node, ui);

            UICreated(token);

            return token;


            void UICreated(UIControlToken<T, TContainer> token)
            {
                combinedPlugin.OnUICreated(name, token, container, parentUI, slotRectConfigs, uiElement.GetTemplate() as TUI);
                onCreated?.Invoke();
            }

        }

        public MikaTask<VirtualUIControlToken<T>> CreateVirtual<T>(IVirtualSlot slot = null) where T : IVirtualUI, new()
        {
            return InternalCreateVirtual<T>(slot?.ParentUI);
        }

        async MikaTask<VirtualUIControlToken<T>> InternalCreateVirtual<T>(IUI parentUI) where T : IVirtualUI, new()
        {
            var virtualUIElement = uiElementProvider.GetVirtualUIElement<T>();
            (var virtualUI, var onCreated, var elementId) = await virtualUIElement.Create();

            if (virtualUI is T ui == false)
            {
                throw new Exception($"Cannot create UI {typeof(T).Name}");
            }
            VirtualUIControlToken<T> token = null;
            Node node = null;
            token = new()
            {
                TokenID = Guid.NewGuid(),
                ElementID = elementId,
                UI = ui,
                Name = typeof(T).Name,
                Recovery = () => UIFullTreeRecovery(node),
            };

            node = new(
                token.TokenID,
                token.Name
            )
            {
                BeforeRecoverySelf = () =>
                {
                    node.ChangeStatus(from: NodeStatus.Created, to: NodeStatus.BeforeRecoveryed);
                },
                AfterRecoverySelf = () =>
                {
                    nodeManager.DetachNode(node, ui);
                    virtualUIElement.Recovery(ui);
                },
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
                logger?.LogError?.Invoke(e);
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

                node.BeforeRecoverySelf();

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

                node.AfterRecoverySelf();
            }

        }

        public virtual async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            foreach (var node in nodeManager.GetRootNodes())
            {
                UIFullTreeRecovery(node);
            }

            nodeManager.Dispose();
        }
    }


}
