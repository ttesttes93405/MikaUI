using System;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;

namespace Tests.Core
{
    public class NodeIdentityTests
    {
        public sealed record RecordVirtualUI : IVirtualUI
        {
            public int Value { get; set; }
        }

        static TestableUIManager CreateManager(FakeUIElementProvider provider) =>
            new(provider, new FakeCanvasProvider(),
                Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

        [Test]
        public void EqualVirtualInstances_HaveIndependentTrees()
        {
            var provider = new FakeUIElementProvider();
            using var manager = CreateManager(provider);
            var first = manager.CreateVirtual<RecordVirtualUI>().WaitResult();
            var second = manager.CreateVirtual<RecordVirtualUI>().WaitResult();
            Assert.That(second.UI, Is.EqualTo(first.UI));
            Assert.That(second.UI, Is.Not.SameAs(first.UI));

            var firstChild = manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(first.UI)).WaitResult();
            var secondChild = manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(second.UI)).WaitResult();

            first.Dispose();

            Assert.That(firstChild.IsDisposed, Is.True);
            Assert.That(second.IsDisposed, Is.False);
            Assert.That(secondChild.IsDisposed, Is.False);
            second.Dispose();
            Assert.That(secondChild.IsDisposed, Is.True);
            Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(4));
        }

        [Test]
        public void MutatedVirtualInstance_RemainsAParentAndIsRemovedOnRecovery()
        {
            var provider = new FakeUIElementProvider();
            using var manager = CreateManager(provider);
            var parent = manager.CreateVirtual<RecordVirtualUI>().WaitResult();
            var ui = parent.UI;
            ui.Value = 42;

            var virtualChild = manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(ui)).WaitResult();
            var visualChild = manager.Create<DummyUI>(new DummySlot(new DummyContainer(), ui)).WaitResult();
            parent.Dispose();

            Assert.That(virtualChild.IsDisposed, Is.True);
            Assert.That(visualChild.IsDisposed, Is.True);
            Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(2));
            Assert.That(provider.RecoveryCount, Is.EqualTo(1));

            // Restoring the original value must not expose a stale dictionary entry.
            ui.Value = 0;
            var error = Assert.Throws<AggregateException>(() =>
                manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(ui)).WaitResult());
            Assert.That(error.InnerException.Message, Does.Contain("Parent UI"));
            Assert.That(error.InnerException.Message, Does.Contain("not found"));
        }

        [Test]
        public void EqualUnregisteredInstance_CannotBeUsedAsParent()
        {
            var provider = new FakeUIElementProvider();
            using var manager = CreateManager(provider);
            var parent = manager.CreateVirtual<RecordVirtualUI>().WaitResult();
            var unregistered = new RecordVirtualUI();
            Assert.That(unregistered, Is.EqualTo(parent.UI));

            var error = Assert.Throws<AggregateException>(() =>
                manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(unregistered)).WaitResult());

            Assert.That(error.InnerException.Message, Does.Contain("Parent UI"));
            Assert.That(error.InnerException.Message, Does.Contain("not found"));
            Assert.That(parent.IsDisposed, Is.False);
        }
    }
}
