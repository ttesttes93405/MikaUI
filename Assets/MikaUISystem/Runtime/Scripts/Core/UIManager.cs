
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

        public ICanvasProvider CanvasProvider { get; private set; }


        record MountInfo
        {
            public Guid TokenID { get; init; }
            public IVirtualUI UI { get; init; }

            public Action<UITokenStatus> OnChangeRecoveryStatus { get; init; }
            public Func<MikaTask> AfterRecoverySelf { get; init; }
            public Func<MikaTask> BeforeRecoverySelf { get; init; }
            public Func<MikaTask> FullRecovery { get; init; }
        }

        record MountInfoRepo
        {
            public MountInfo MountInfo { get; set; }
            public List<MountInfo> Children { get; init; }
        }

        readonly Dictionary<Guid, MountInfoRepo> mountLinkQuery = new();
        readonly Dictionary<IUI, Guid> mountLinkQueryByUI = new();

        readonly PluginCombiner<TUI, TContainer> combinedPlugin;
        readonly Logger logger;
        public UIManager(IUIElementProvider<TUI, TContainer> uiElementProvider, ICanvasProvider canvasProvider, IEnumerable<IPlugin<TUI, TContainer>> plugins, Logger logger)
        {
            this.uiElementProvider = uiElementProvider;
            CanvasProvider = canvasProvider;
            this.logger = logger;


            combinedPlugin = new PluginCombiner<TUI, TContainer>(plugins);

            combinedPlugin.Install(this);
        }


        public MikaTask<UILifeToken<T>> Create<T>(ISlot<TContainer> slot, SlotRectConfigs slotRectConfigs = null, string name = "") where T : class, TUI, IUI
        {
            try
            {
                return InternalCreate<T>(name, slot.Container, slot.ParentUI, null, slotRectConfigs);
            }
            catch (Exception e)
            {
                logger?.LogError?.Invoke(e);
                throw e;
            }
        }

        protected async MikaTask<UILifeToken<T>> InternalCreate<T>(string name, TContainer container, IUI parentUI, int? sortingOrder, SlotRectConfigs slotRectConfigs) where T : class, TUI, IUI
        {
            var uiElement = uiElementProvider.GetUIElement<T>(name);

            if (uiElement == null)
            {
                throw new Exception($"Cannot find UIElement: {typeof(T).Name}");
            }

            (var uiIns, var onCreated, var elementID) = await uiElement.Create(container);

            if (uiIns is T == false)
            {
                throw new Exception($"Cannot create UI {typeof(T).Name}");
            }

            T ui = uiIns as T;

            MountInfo mountInfo = null;
            UILifeToken<T> token = null;
            token = new()
            {
                ElementID = elementID,
                TokenID = Guid.NewGuid(),
                UI = ui,
                Recovery = UIFullTreeRecovery,
                BeforeRecovery = UITreeBeforeRecovery,
                AfterRecovery = UITreeAfterRecovery,
                SortingOrder = sortingOrder,
                RecoveryStatus = UITokenStatus.None,
            };

            mountLinkQueryByUI.Add(ui, token.TokenID);

            mountInfo = new()
            {
                TokenID = token.TokenID,
                OnChangeRecoveryStatus = (status) => token.RecoveryStatus = status,
                UI = ui,
                AfterRecoverySelf = UIAfterRecoverySelf,
                BeforeRecoverySelf = UIBeforeRecoverySelf,
                FullRecovery = UIFullTreeRecovery,
            };

            DoAddMountLink(parentUI, mountInfo);

            await UICreated();

            return token;


            async MikaTask UICreated()
            {

                if (token.SortingOrder.HasValue)
                {
                    CanvasProvider.Registry(token.TokenID, token.SortingOrder.Value);
                }

                combinedPlugin.OnUICreated(name, token, container, parentUI, slotRectConfigs, uiElement.GetTemplate() as TUI);

                onCreated?.Invoke();

            }

            async MikaTask UIFullTreeRecovery()
            {
                if (token.RecoveryStatus != UITokenStatus.None)
                    return;

                try
                {
                    await UITreeBeforeRecovery();
                    await UITreeAfterRecovery();
                }
                catch (Exception e)
                {
                    logger?.LogError?.Invoke(e);
                }
            }

            async MikaTask UITreeBeforeRecovery()
            {
                if (token.RecoveryStatus != UITokenStatus.None)
                    return;

                await TreeBeforeRecovery(token.TokenID, mountInfo.OnChangeRecoveryStatus);
            }

            async MikaTask UITreeAfterRecovery()
            {
                if (token.RecoveryStatus != UITokenStatus.BeforeRecoveryed)
                    return;

                await TreeAfterRecovery(token.TokenID);
            }

            async MikaTask UIBeforeRecoverySelf()
            {
                if (token.RecoveryStatus != UITokenStatus.None)
                    return;


                if (ui is IUIAsyncRecoverable uiAsyncRecoverable)
                {
                    token.RecoveryStatus = UITokenStatus.BeforeRecoverying;

                    await uiAsyncRecoverable.OnRecovery();

                    token.RecoveryStatus = UITokenStatus.BeforeRecoveryed;
                    return;
                }
                else
                {
                    token.RecoveryStatus = UITokenStatus.BeforeRecoveryed;
                    return;
                }
            }

            async MikaTask UIAfterRecoverySelf()
            {
                if (token.RecoveryStatus != UITokenStatus.BeforeRecoveryed)
                    return;

                token.RecoveryStatus = UITokenStatus.AfterRecoverying;

                OnUILifeTokenRecovery(token);

                DoRemoveMountLink(parentUI, mountInfo);

                combinedPlugin.OnUIWillRecovery(name, token);

                mountLinkQueryByUI.Remove(ui);
                await uiElement.Recovery(ui);

                combinedPlugin.OnUIRecoveryed(name, token);

                token.RecoveryStatus = UITokenStatus.Recoveryed;

                void OnUILifeTokenRecovery(UILifeToken<T> target)
                {
                    if (target.SortingOrder.HasValue)
                    {
                        CanvasProvider.Unregistry(target.TokenID);
                    }
                }
            }

        }




        public MikaTask<VirtualUILifeToken<T>> CreateVirtual<T>(IVirtualSlot slot = null) where T : IVirtualUI, new()
        {
            return __CreateVirtual<T>(slot?.ParentUI);
        }

        async MikaTask<VirtualUILifeToken<T>> __CreateVirtual<T>(IUI parentUI) where T : IVirtualUI, new()
        {
            var virtualUIElement = uiElementProvider.GetVirtualUIElement<T>();
            (var virtualUI, var onCreated, var elementId) = await virtualUIElement.Create();

            UITokenStatus recoveryStatus = UITokenStatus.None;
            MountInfo mountInfo = null;
            VirtualUILifeToken<T> token = null;
            if (virtualUI is T ui)
            {
                token = new()
                {
                    ElementID = elementId,
                    TokenID = Guid.NewGuid(),
                    UI = ui,
                    BeforeRecovery = VirtaulUIBeforeRecoverySelf,
                    AfterRecovery = VirtualUIAfterRecoverySelf,
                    Recovery = VirtaulUIRecovery,
                };

                mountInfo = new()
                {
                    TokenID = token.TokenID,
                    OnChangeRecoveryStatus = (status) => { },
                    UI = ui,
                    AfterRecoverySelf = token.AfterRecovery,
                    BeforeRecoverySelf = token.BeforeRecovery
                };

                DoAddMountLink(parentUI, mountInfo);

                await VirtualUICreated();

                return token;
            }

            throw new Exception($"Cannot create UI {typeof(T).Name}");

            async MikaTask VirtualUICreated()
            {
                onCreated?.Invoke();
                combinedPlugin.OnVirtualUICreated(token, parentUI);
            }

            async MikaTask VirtaulUIRecovery()
            {
                if (recoveryStatus != UITokenStatus.None)
                    return;

                recoveryStatus = UITokenStatus.BeforeRecoverying;

                try
                {
                    await VirtaulUIBeforeRecoverySelf();
                    await VirtualUIAfterRecoverySelf();
                }
                catch (Exception e)
                {
                    logger?.LogError?.Invoke(e);
                }

                recoveryStatus = UITokenStatus.Recoveryed;

            }


            async MikaTask VirtaulUIBeforeRecoverySelf()
            {
                if (ui is IUIAsyncRecoverable uiAsyncRecoverable)
                {
                    await uiAsyncRecoverable.OnRecovery();
                }
            }

            async MikaTask VirtualUIAfterRecoverySelf()
            {
                DoRemoveMountLink(parentUI, mountInfo);

                if (virtualUI is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }


        }




        /// <summary>
        /// Call all children UI's BeforeRecovery(Exit), include parentUI
        /// </summary>
        async MikaTask TreeBeforeRecovery(Guid tokenID, Action<UITokenStatus> onChangeRecoveryStatus)
        {
            if (tokenID == null)
                return;

            List<MikaTask> tasks = new();

            ExitChildrenUIs(tokenID, tasks);

            if (mountLinkQuery.TryGetValue(tokenID, out var mountInfoRepo))
            {
                MikaTask BeforeRecoverySelf = mountInfoRepo.MountInfo.BeforeRecoverySelf();
                tasks.Add(BeforeRecoverySelf);
            }
            else
            {
                logger?.LogError?.Invoke($"Cannot find mountInfoRepo for {tokenID}");
            }

            await MikaTask.WhenAll(tasks);

            void ExitChildrenUIs(Guid tokenID, List<MikaTask> tasks)
            {
                if (mountLinkQuery.TryGetValue(tokenID, out var mountInfoRepo))
                {
                    foreach (var mountInfo in mountInfoRepo.Children)
                    {
                        MikaTask task = mountInfo.BeforeRecoverySelf();
                        tasks.Add(task);

                        if (mountInfo.UI is IUI ui)
                        {
                            ExitChildrenUIs(mountInfo.TokenID, tasks);
                        }
                    }
                }

            }
        }

        /// <summary>
        /// Call all children UI's AfterRecovery(Recovery), include parentUI
        /// </summary>
        async MikaTask TreeAfterRecovery(Guid TokenID)
        {
            if (TokenID == null)
                return;

            await RecoveryChildrenUIs(TokenID);

            if (mountLinkQuery.TryGetValue(TokenID, out var mountInfoRepo))
            {
                await mountInfoRepo.MountInfo.AfterRecoverySelf();
            }

            async MikaTask RecoveryChildrenUIs(Guid TokenID)
            {
                if (mountLinkQuery.TryGetValue(TokenID, out var mountInfoRepo))
                {
                    foreach (var mountInfo in mountInfoRepo.Children)
                    {
                        if (mountInfo.UI is IUI ui)
                        {
                            await RecoveryChildrenUIs(mountInfo.TokenID);
                        }
                    }

                    var tasks = mountInfoRepo.Children
                        .ToArray()  // make a copy
                        .Select(r => r.AfterRecoverySelf.Invoke());

                    await MikaTask.WhenAll(tasks);
                }
            }
        }



        readonly Dictionary<Guid, MountInfoRepo> noParentMountLinkQuery = new();

        void DoAddMountLink(IUI parentUI, MountInfo mountInfo)
        {
            var mountRepo = new MountInfoRepo()
            {
                MountInfo = mountInfo,
                Children = new(),
            };
            mountLinkQuery.Add(mountInfo.TokenID, mountRepo);


            if (parentUI == null)
            {
                noParentMountLinkQuery.Add(mountInfo.TokenID, mountRepo);
                return;
            }

            if (mountLinkQueryByUI.TryGetValue(parentUI, out var parentTokenID))
            {
                if (mountLinkQuery.TryGetValue(parentTokenID, out var repo))
                {
                    repo.Children.Add(mountInfo);
                }
            }


        }

        void DoRemoveMountLink(IUI parentUI, MountInfo mountInfo)
        {
            mountLinkQuery.Remove(mountInfo.TokenID);

            if (parentUI == null)
            {
                noParentMountLinkQuery.Remove(mountInfo.TokenID);
                return;
            }

            if (mountLinkQueryByUI.TryGetValue(parentUI, out var parentTokenID))
            {
                if (mountLinkQuery.TryGetValue(parentTokenID, out var repo))
                {
                    repo.Children.Remove(mountInfo);
                }
            }

        }

        public virtual async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            foreach (var repo in noParentMountLinkQuery.Values.ToArray())
            {
                await repo.MountInfo.FullRecovery();
            }

            mountLinkQuery.Clear();
            mountLinkQueryByUI.Clear();
            noParentMountLinkQuery.Clear();

        }
    }


}