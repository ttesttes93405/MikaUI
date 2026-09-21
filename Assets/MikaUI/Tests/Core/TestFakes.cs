using System;
using System.Collections.Generic;
using MikaUI;
using MikaUI.Plugin;

namespace Tests.Core
{

    internal sealed class TestableUIManager : UIManager<DummyUI, DummyContainer, object>
    {

        public ICanvasProvider CanvasProvider { get; private set; }
        public TestableUIManager(
            IUIElementProvider<DummyUI, DummyContainer> uiElementProvider,
            ICanvasProvider canvasProvider,
            IEnumerable<IPlugin<DummyUI, DummyContainer, object>> plugins,
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
            CanvasProvider.Register(token.TokenID, sortingOrder);
            token.OnDispose += () =>
            {
                CanvasProvider.Unregister(token.TokenID);
            };
            return token;
        }
    }

    internal sealed class FakeCanvasProvider : ICanvasProvider
    {
        private readonly HashSet<Guid> registered = new();

        public IReadOnlyCollection<Guid> RegisteredIds => registered;

        public float DefaultScaleFactor => 1f;

        public void Registry(Guid id, int sortingOrder) => Register(id, sortingOrder);

        public void Unregistry(Guid id) => Unregister(id);

        public void Register(Guid id, int sortingOrder)
        {
            registered.Add(id);
        }

        public void Unregister(Guid id)
        {
            registered.Remove(id);
        }
    }

    internal sealed class FakeUIElementProvider : IUIElementProvider<DummyUI, DummyContainer>
    {
        public List<DummyUI> CreatedInstances { get; } = new();
        public int RecoveryCount { get; private set; }
        public int VirtualRecoveryCount { get; private set; }

        public UIElement<DummyContainer> GetUIElement<T>(string name) where T : DummyUI, IVisualUI
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

                    return MikaTask<(IVisualUI ui, Action onCreated, Guid elementID)>.FromResult((ui, () => { }, elementId));
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
        Recovered = 3,
        VirtualCreated = 4,
    }

    internal sealed class RecordingPlugin :
        IPlugin<DummyUI, DummyContainer, object>,
        IPluginUICreatedHandler<DummyUI, DummyContainer, object>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveredHandler,
        IPluginVirtualUICreatedHandler
    {
        public List<EventType> Events { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }

        public void Uninstall(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, DummyContainer> token, DummyContainer container, IBaseUI parentUI, object slotRectConfigs, DummyUI template) where T : DummyUI, IVisualUI
        {
            Events.Add(EventType.Created);
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IVisualUI
        {
            Events.Add(EventType.WillRecovery);
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            Events.Add(EventType.Recovered);
        }

        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : IVirtualUI, new()
        {
            Events.Add(EventType.VirtualCreated);
        }

    }

    internal class DummyContainer { }

    internal class DummyUI : IVisualUI
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
        public static DummySlot Child(IVisualUI parentUI) => new DummySlot(new DummyContainer(), parentUI);

        public DummySlot(DummyContainer container, IVisualUI parentUI)
        {
            Container = container;
            ParentUI = parentUI;
        }

        public DummyContainer Container { get; }
        public IBaseUI ParentUI { get; }
    }

    internal sealed class NullUIElementProvider : IUIElementProvider<DummyUI, DummyContainer>
    {
        public UIElement<DummyContainer> GetUIElement<T>(string name) where T : DummyUI, IVisualUI
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
        Recovered,
    }

    internal sealed class ThrowingRecoveryPlugin :
        IPlugin<DummyUI, DummyContainer, object>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveredHandler
    {
        public Guid ThrowForTokenID { get; set; }
        public List<Guid> WillRecoveryTokenIds { get; } = new();
        public List<Guid> RecoveredTokenIds { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }

        public void Uninstall(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IVisualUI
        {
            WillRecoveryTokenIds.Add(token.TokenID);

            if (token.TokenID == ThrowForTokenID)
            {
                throw new InvalidOperationException($"Simulated recovery failure for {name}");
            }
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            RecoveredTokenIds.Add(tokenID);
        }
    }

    internal sealed class ThrowingCreatePlugin :
        IPlugin<DummyUI, DummyContainer, object>,
        IPluginUICreatedHandler<DummyUI, DummyContainer, object>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveredHandler
    {
        public List<EventType> Events { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer, object> manager) { }

        public void Uninstall(UIManager<DummyUI, DummyContainer, object> manager) { }

        public void OnUICreated<T>(string name, UIControlToken<T, DummyContainer> token, DummyContainer container, IBaseUI parentUI, object slotRectConfigs, DummyUI template) where T : DummyUI, IVisualUI
        {
            Events.Add(EventType.Created);
            throw new InvalidOperationException("Simulated create failure.");
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IVisualUI
        {
            Events.Add(EventType.WillRecovery);
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            Events.Add(EventType.Recovered);
        }
    }

    internal sealed class TreeRecordingPlugin :
        IPlugin<DummyUI, DummyContainer, object>,
        IPluginUICreatedHandler<DummyUI, DummyContainer, object>,
        IPluginUIWillRecoveryHandler<DummyUI, DummyContainer>,
        IPluginUIRecoveredHandler
    {
        public List<(TreePluginEventType EventType, Guid TokenId)> Events { get; } = new();

        public int SortingOrder => 0;

        public void Install(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }

        public void Uninstall(UIManager<DummyUI, DummyContainer, object> manager)
        {
        }
        
        public void OnUICreated<T>(string name, UIControlToken<T, DummyContainer> token, DummyContainer container, IBaseUI parentUI, object slotRectConfigs, DummyUI template) where T : DummyUI, IVisualUI
        {
            Events.Add((TreePluginEventType.Created, token.TokenID));
        }

        public void OnUIWillRecovery<T>(string name, UIControlToken<T, DummyContainer> token) where T : DummyUI, IVisualUI
        {
            Events.Add((TreePluginEventType.WillRecovery, token.TokenID));
        }

        public void OnUIRecovered(string name, Guid tokenID)
        {
            Events.Add((TreePluginEventType.Recovered, tokenID));
        }
    }
}
