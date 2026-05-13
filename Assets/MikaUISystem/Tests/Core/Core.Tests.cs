using System;
using System.Linq;
using NUnit.Framework;
using MikaUISystem.Plugin;

namespace Tests.Core
{

    public class CoreTests
    {
        [Test]
        public void CreateWithSorting_DisposesUiAndUnregistersCanvas()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new RecordingPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new[] { plugin }, DummyLogger.Create());

            try
            {
                var token = manager.CreateWithSorting().WaitResult();

                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(1));
                Assert.That(canvasProvider.RegisteredIds.Contains(token.TokenID), Is.True);
                Assert.That(provider.CreatedInstances.Count, Is.EqualTo(1));
                Assert.That(plugin.Events.Single(), Is.EqualTo(EventType.Created));

                token.Dispose();

                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { EventType.Created, EventType.WillRecovery, EventType.Recoveryed }, plugin.Events);
            }
            finally
            {
                manager.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void CreateVirtual_AssignsUniqueIdsAndRunsHandlers()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new RecordingPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new[] { plugin }, DummyLogger.Create());

            try
            {
                var firstToken = manager.CreateVirtual<DummyVirtualUI>().WaitResult();
                var secondToken = manager.CreateVirtual<DummyVirtualUI>().WaitResult();

                Assert.That(firstToken.ElementID, Is.Not.EqualTo(secondToken.ElementID));
                Assert.That(plugin.Events.Count(e => e == EventType.VirtualCreated), Is.EqualTo(2));

                firstToken.Dispose();
                secondToken.Dispose();

                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(2));
            }
            finally
            {
                manager.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void CreateSlotWithoutSorting_DoesNotRegisterCanvas()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer>>(), DummyLogger.Create());

            try
            {
                var slot = DummySlot.Root();
                var token = manager.Create<DummyUI>(slot, name: "Root").WaitResult();

                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));
                Assert.That(provider.CreatedInstances.Count, Is.EqualTo(1));

                token.Dispose();

                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));
            }
            finally
            {
                manager.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void Create_WhenUIElementMissing_ThrowsHelpfulException()
        {
            var provider = new NullUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer>>(), DummyLogger.Create());

            try
            {
                var slot = DummySlot.Root();
                var ex = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(slot).WaitResult());
                Assert.That(ex.InnerExceptions[0], Is.TypeOf<NullReferenceException>());
            }
            finally
            {
                manager.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void DisposingParent_RecoversChildBeforeParent()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new TreeRecordingPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer>[] { plugin }, DummyLogger.Create());

            try
            {
                var parentToken = manager.CreateWithSorting(name: "Parent").WaitResult();
                var childSlot = DummySlot.Child(parentToken.UI);
                var childToken = manager.Create<DummyUI>(childSlot, name: "Child").WaitResult();

                parentToken.Dispose();

                var expected = new[]
                {
                    (TreePluginEventType.Created, parentToken.TokenID),
                    (TreePluginEventType.Created, childToken.TokenID),
                    (TreePluginEventType.WillRecovery, childToken.TokenID),
                    (TreePluginEventType.WillRecovery, parentToken.TokenID),
                    (TreePluginEventType.Recoveryed, childToken.TokenID),
                    (TreePluginEventType.Recoveryed, parentToken.TokenID),
                };

                CollectionAssert.AreEqual(expected, plugin.Events);

                Assert.That(provider.RecoveryCount, Is.EqualTo(2));
                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));
                GC.KeepAlive(childToken);
            }
            finally
            {
                manager.DisposeAsync().GetAwaiter().GetResult();
            }
        }

    }
}
