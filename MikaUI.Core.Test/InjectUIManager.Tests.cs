using System;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;

namespace Tests.Core
{
    public class InjectUIManagerTests
    {
        [Test]
        public void VirtualUiReceivesManagerInPrivateAncestorField()
        {
            using var manager = CreateManager(new FakeUIElementProvider());
            var token = manager.CreateVirtual<DerivedVirtualUI>().WaitResult();

            Assert.That(token.UI.InjectedManager, Is.SameAs(manager));
            Assert.That(token.UI.UnrelatedField, Is.Null);
            Assert.That(token.UI.IncompatibleManager, Is.Null);
        }

        [Test]
        public void VisualUiReceivesManagerInPrivateAncestorField()
        {
            var ui = new DerivedVisualUI();
            using var manager = CreateManager(new FakeUIElementProvider { VisualResultOverride = ui });
            var token = manager.Create<DummyUI>(DummySlot.Root()).WaitResult();

            Assert.That(token.UI, Is.SameAs(ui));
            Assert.That(ui.InjectedManager, Is.SameAs(manager));
            Assert.That(ui.Manager, Is.Null); // Only the first compatible field is injected.
        }

        [Test]
        public void MostDerivedManagerFieldTakesPrecedence()
        {
            using var manager = CreateManager(new FakeUIElementProvider());
            var token = manager.CreateVirtual<OverridesVirtualUI>().WaitResult();

            Assert.That(token.UI.DerivedManager, Is.SameAs(manager));
            Assert.That(token.UI.InjectedManager, Is.Null);
        }

        static TestableUIManager CreateManager(FakeUIElementProvider provider) =>
            new(provider, new FakeCanvasProvider(),
                new[] { new InjectUIManagerPlugin<DummyUI, DummyContainer, object>() }, DummyLogger.Create());

        // These fields are populated through reflection by the injection plugin.
#pragma warning disable CS0649
        public class BaseVirtualUI : IVirtualUI
        {
            private UIManager<DummyUI, DummyContainer, object> manager;
            internal object InjectedManager => manager;
        }

        public class MiddleVirtualUI : BaseVirtualUI { }

        public class DerivedVirtualUI : MiddleVirtualUI
        {
            private object unrelated;
            private UIManager<object, object, object> incompatibleManager;
            internal object UnrelatedField => unrelated;
            internal object IncompatibleManager => incompatibleManager;
        }

        public class OverridesVirtualUI : BaseVirtualUI
        {
            private UIManager<DummyUI, DummyContainer, object> manager;
            internal object DerivedManager => manager;
        }

        class BaseVisualUI : DummyUI
        {
            private UIManager<DummyUI, DummyContainer, object> manager;
            public object InjectedManager => manager;
        }

        class DerivedVisualUI : BaseVisualUI { }
#pragma warning restore CS0649
    }
}
