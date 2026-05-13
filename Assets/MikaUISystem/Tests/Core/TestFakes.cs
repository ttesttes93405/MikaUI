using System;
using System.Collections.Generic;
using MikaUISystem;
using MikaUISystem.Plugin;

namespace Tests.Core
{

    internal sealed class TestableUIManager : UIManager<DummyUI, DummyContainer>
    {

        public ICanvasProvider CanvasProvider { get; private set; }
        public TestableUIManager(
            IUIElementProvider<DummyUI, DummyContainer> uiElementProvider,
            ICanvasProvider canvasProvider,
            IEnumerable<IPlugin<DummyUI, DummyContainer>> plugins,
            Logger logger)
            : base(uiElementProvider, plugins, logger)
        {
            CanvasProvider = canvasProvider;
        }

        public async MikaTask<UIControlToken<DummyUI, DummyContainer>> CreateWithSorting(int sortingOrder = 1, string name = "Test")
        {
            var container = new DummyContainer();
            DummySlot slot = new(container, parentUI: null);
            var token = await Create<DummyUI>(slot, slotRectConfigs: null, name: name);
            CanvasProvider.Registry(token.TokenID, sortingOrder);
            token.OnDispose += () =>
            {
                CanvasProvider.Unregistry(token.TokenID);
            };
            return token;
        }
    }

    internal sealed class FakeCanvasProvider : ICanvasProvider
    {
        private readonly HashSet<Guid> registered = new();

        public IReadOnlyCollection<Guid> RegisteredIds => registered;

        public float DefaultScaleFactor => 1f;

        public void Registry(Guid id, int sortingOrder)
        {
            registered.Add(id);
        }

        public void Unregistry(Guid id)
        {
            registered.Remove(id);
        }
    }

    internal sealed class FakeUIElementProvider : IUIElementProvider<DummyUI, DummyContainer>
    {
        public List<DummyUI> CreatedInstances { get; } = new();
        public int RecoveryCount { get; private set; }
        public int VirtualRecoveryCount { get; private set; }

        public UIElement<DummyContainer> GetUIElement<T>(string name) where T : DummyUI, IUI
        {
            if (typeof(T) != typeof(DummyUI))
            {
                return null;
            }

            return new UIElement<DummyContainer>
            {
                UIName = "TestUI",
                GetTemplate = () => new DummyUI(),
                Create = container =>
                {
                    var ui = new DummyUI();
                    CreatedInstances.Add(ui);
                    var elementId = Guid.NewGuid();

                    return MikaTask<(IUI ui, Action onCreated, Guid elementID)>.FromResult((ui, () => { }, elementId));
                },
                Recovery = ui => { RecoveryCount++; },
            };
        }

        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new()
        {
            return new VirtualUIElement
            {
                UIName = typeof(T).Name,
                Create = () =>
                {
                    var ui = new T();
                    return MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>.FromResult((ui, () => { }, Guid.NewGuid()));
                },
                Recovery = ui => { VirtualRecoveryCount++; },
            };
        }
    }


    public enum EventType
    {
        Created = 1,
        WillRecovery = 2,
        Recoveryed = 3,
        VirtualCreated = 4,
    }

    internal sealed class RecordingPlugin :
        IPlugin<DummyUI, DummyContainer>,
        IPluginUICreatedHandler<DummyUI, DummyContainer>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveryedHandler,
        IPluginVirtualUICreatedHandler
    {
        public List<EventType> Events { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, DummyContainer> token, DummyContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, DummyUI template) where T : DummyUI, IUI
        {
            Events.Add(EventType.Created);
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IUI
        {
            Events.Add(EventType.WillRecovery);
        }

        public void OnUIRecoveryed(string name, Guid tokenID)
        {
            Events.Add(EventType.Recoveryed);
        }

        public void OnVirtualUICreated<T>(VirtualUIControlToken<T> token, IUI parentUI) where T : IVirtualUI, new()
        {
            Events.Add(EventType.VirtualCreated);
        }

    }

    internal class DummyContainer { }

    internal class DummyUI : IUI
    {
    }

    internal class DummyVirtualUI : IVirtualUI
    {
    }

    internal static class DummyLogger
    {
        public static Logger Create()
        {
            return new Logger
            {
                Log = _ => { },
                LogWarning = _ => { },
                LogError = _ => { },
            };
        }
    }

    internal sealed class DummySlot : ISlot<DummyContainer>
    {
        public static DummySlot Root() => new DummySlot(new DummyContainer(), null);
        public static DummySlot Child(IUI parentUI) => new DummySlot(new DummyContainer(), parentUI);

        public DummySlot(DummyContainer container, IUI parentUI)
        {
            Container = container;
            ParentUI = parentUI;
        }

        public DummyContainer Container { get; }
        public IUI ParentUI { get; }
    }

    internal sealed class NullUIElementProvider : IUIElementProvider<DummyUI, DummyContainer>
    {
        public UIElement<DummyContainer> GetUIElement<T>(string name) where T : DummyUI, IUI
        {
            return null;
        }

        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new()
        {
            throw new NotSupportedException("Virtual UI elements are not supported in NullUIElementProvider.");
        }
    }

    internal enum TreePluginEventType
    {
        Created,
        WillRecovery,
        Recoveryed,
    }

    internal sealed class TreeRecordingPlugin :
        IPlugin<DummyUI, DummyContainer>,
        IPluginUICreatedHandler<DummyUI, DummyContainer>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveryedHandler
    {
        public List<(TreePluginEventType EventType, Guid TokenId)> Events { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, DummyContainer> token, DummyContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, DummyUI template) where T : DummyUI, IUI
        {
            Events.Add((TreePluginEventType.Created, token.TokenID));
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IUI
        {
            Events.Add((TreePluginEventType.WillRecovery, token.TokenID));
        }

        public void OnUIRecoveryed(string name, Guid tokenID)
        {
            Events.Add((TreePluginEventType.Recoveryed, tokenID));
        }
    }
}
