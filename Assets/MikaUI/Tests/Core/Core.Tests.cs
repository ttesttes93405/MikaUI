using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using MikaUI;
using MikaUI.Plugin;

namespace Tests.Core
{

    public class CoreTests
    {
        [Test]
        public void MikaTask_Await_PropagatesFault()
        {
            var expectedException = new InvalidOperationException("Simulated MikaTask failure.");
            var task = new MikaTask(Task.FromException(expectedException));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await task);

            Assert.That(exception, Is.SameAs(expectedException));
        }

        [Test]
        public void MikaTask_Await_PropagatesCancellation()
        {
            var task = new MikaTask(Task.FromCanceled(new CancellationToken(canceled: true)));

            Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
        }

        [Test]
        public void MikaTaskOfT_FromTask_PropagatesFault()
        {
            var expectedException = new InvalidOperationException("Simulated MikaTask<T> failure.");
            var task = new MikaTask<int>(Task.FromException(expectedException));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await task);

            Assert.That(exception, Is.SameAs(expectedException));
        }

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
                CollectionAssert.AreEqual(new[] { EventType.Created, EventType.WillRecovery, EventType.Recovered }, plugin.Events);
            }
            finally
            {
                manager.Dispose();
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
                manager.Dispose();
            }
        }

        [Test]
        public void CreateSlotWithoutSorting_DoesNotRegisterCanvas()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

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
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenUIElementMissing_ThrowsHelpfulException()
        {
            var provider = new NullUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                var slot = DummySlot.Root();
                var ex = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(slot).WaitResult());
                Assert.That(ex.InnerExceptions[0], Is.TypeOf<NullReferenceException>());
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenProviderLookupFails_LogsAndPropagatesException()
        {
            var expectedException = new InvalidOperationException("Simulated UI lookup failure.");
            var provider = new FakeUIElementProvider { GetUIElementException = expectedException };
            var loggedErrors = new List<object>();
            var logger = new Logger { LogError = loggedErrors.Add };
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), logger);

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());

                Assert.That(exception.InnerExceptions, Has.Count.EqualTo(1));
                Assert.That(exception.InnerExceptions[0], Is.SameAs(expectedException));
                Assert.That(loggedErrors, Has.Count.EqualTo(1));
                Assert.That(loggedErrors[0], Is.SameAs(expectedException));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenManagerIsDisposedDuringLookup_DoesNotCreateAnElement()
        {
            var provider = new DeferredUIElementProvider();
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                var createTask = manager.Create<DummyUI>(DummySlot.Root());

                manager.Dispose();
                provider.LookupSource.SetResult(provider.CreateElement());

                var exception = Assert.Throws<AggregateException>(() => createTask.WaitResult());
                Assert.That(exception.InnerExceptions[0], Is.TypeOf<ObjectDisposedException>());
                Assert.That(provider.CreateInvocationCount, Is.EqualTo(0));
                Assert.That(provider.RecoveryCount, Is.EqualTo(0));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenManagerIsDisposedDuringElementCreation_RecoversTheElement()
        {
            var provider = new DeferredUIElementProvider();
            provider.LookupSource.SetResult(provider.CreateElement());
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                var createTask = manager.Create<DummyUI>(DummySlot.Root());
                Assert.That(provider.CreateInvocationCount, Is.EqualTo(1));

                manager.Dispose();
                provider.CreateSource.SetResult((new DummyUI(), null, Guid.NewGuid()));

                var exception = Assert.Throws<AggregateException>(() => createTask.WaitResult());
                Assert.That(exception.InnerExceptions[0], Is.TypeOf<ObjectDisposedException>());
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenPluginDisposesManager_DoesNotReturnAnAlreadyDisposedToken()
        {
            var provider = new FakeUIElementProvider();
            var plugin = new DisposingCreatePlugin();
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());

                Assert.That(exception.InnerExceptions[0], Is.TypeOf<ObjectDisposedException>());
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                Assert.That(manager.IsDisposed, Is.True);
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Dispose_IsIdempotent_AndFutureCreatesFail()
        {
            var provider = new FakeUIElementProvider();
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            Assert.DoesNotThrow(() => manager.Dispose());
            Assert.DoesNotThrow(() => manager.Dispose());

            var exception = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());
            Assert.That(exception.InnerExceptions[0], Is.TypeOf<ObjectDisposedException>());
            Assert.That(provider.CreatedInstances, Is.Empty);
        }

        [Test]
        public void Create_InjectsDerivedManagerIntoBaseManagerField()
        {
            var provider = new FakeUIElementProvider();
            var plugin = new InjectUIManagerPlugin<DummyUI, DummyContainer, object>();
            var manager = new TestableUIManager(
                provider,
                new FakeCanvasProvider(),
                new IPlugin<DummyUI, DummyContainer, object>[] { plugin },
                DummyLogger.Create());

            try
            {
                var token = manager.Create<DummyUI>(DummySlot.Root()).WaitResult();

                Assert.That(token.UI.Manager, Is.SameAs(manager));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Dispose_WhenPluginUninstallThrows_UninstallsLaterPluginsAndLogsFailure()
        {
            var loggedErrors = new List<object>();
            var laterPlugin = new RecordingUninstallPlugin(sortingOrder: 0);
            var throwingPlugin = new ThrowingUninstallPlugin(sortingOrder: 1);
            var manager = new TestableUIManager(
                new FakeUIElementProvider(),
                new FakeCanvasProvider(),
                new IPlugin<DummyUI, DummyContainer, object>[] { laterPlugin, throwingPlugin },
                new Logger { LogError = loggedErrors.Add });

            Assert.DoesNotThrow(() => manager.Dispose());
            Assert.That(laterPlugin.UninstallCount, Is.EqualTo(1));
            Assert.That(loggedErrors.Single(), Is.SameAs(throwingPlugin.Exception));
        }

        [Test]
        public void CreateVirtual_WhenProviderLookupFails_LogsAndPropagatesException()
        {
            var expectedException = new InvalidOperationException("Simulated virtual UI lookup failure.");
            var provider = new FakeUIElementProvider { GetVirtualUIElementException = expectedException };
            var loggedErrors = new List<object>();
            var logger = new Logger { LogError = loggedErrors.Add };
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), logger);

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.CreateVirtual<DummyVirtualUI>().WaitResult());

                Assert.That(exception.InnerExceptions, Has.Count.EqualTo(1));
                Assert.That(exception.InnerExceptions[0], Is.SameAs(expectedException));
                Assert.That(loggedErrors, Has.Count.EqualTo(1));
                Assert.That(loggedErrors[0], Is.SameAs(expectedException));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenCreatedPluginThrows_RollsBackTheNodeAndElement()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingCreatePlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());

                Assert.That(provider.CreatedInstances.Count, Is.EqualTo(1));
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(
                    new[] { EventType.Created, EventType.WillRecovery, EventType.Recovered },
                    plugin.Events);

                // A second recovery during manager disposal would mean the failed node
                // was retained in the lifecycle tree.
                manager.Dispose();
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Create_WhenRecoveryErrorObserverThrows_StillCompletesRollback()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingCreatePlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());
            manager.OnRecoveryError += _ => throw new InvalidOperationException("Simulated observer failure.");

            try
            {
                Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());

                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(
                    new[] { EventType.Created, EventType.WillRecovery, EventType.Recovered },
                    plugin.Events);

                manager.Dispose();
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void CreateVirtual_WhenCreatedPluginThrows_RollsBackTheNodeAndElement()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingVirtualCreatePlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                Assert.Throws<AggregateException>(() => manager.CreateVirtual<DummyVirtualUI>().WaitResult());
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));

                manager.Dispose();
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void CreateVirtual_WhenOnCreatedThrows_RollsBackTheNodeAndElement()
        {
            var provider = new FakeUIElementProvider { ThrowOnVirtualCreated = true };
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                Assert.Throws<AggregateException>(() => manager.CreateVirtual<DummyVirtualUI>().WaitResult());
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));

                manager.Dispose();
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void Dispose_WhenRecoveryErrorObserverThrows_StillCompletesRecovery()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingRecoveryPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                var token = manager.CreateWithSorting().WaitResult();
                plugin.ThrowForTokenID = token.TokenID;
                manager.OnRecoveryError += _ => throw new InvalidOperationException("Simulated observer failure.");

                Assert.DoesNotThrow(() => token.Dispose());
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                Assert.That(token.IsDisposed, Is.True);
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void DisposingParent_RecoversChildBeforeParent()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new TreeRecordingPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                var parentToken = manager.CreateWithSorting(name: "Parent").WaitResult();
                var childSlot = DummySlot.Child(parentToken.UI);
                var childToken = manager.Create<DummyUI>(childSlot, name: "Child").WaitResult();

                var parentTokenID = parentToken.TokenID;
                var childTokenID = childToken.TokenID;

                parentToken.Dispose();

                var expected = new[]
                {
                    (TreePluginEventType.Created, parentTokenID),
                    (TreePluginEventType.Created, childTokenID),
                    (TreePluginEventType.WillRecovery, childTokenID),
                    (TreePluginEventType.WillRecovery, parentTokenID),
                    (TreePluginEventType.Recovered, childTokenID),
                    (TreePluginEventType.Recovered, parentTokenID),
                };

                CollectionAssert.AreEqual(expected, plugin.Events);

                Assert.That(provider.RecoveryCount, Is.EqualTo(2));
                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));

                // A child recovered as part of its parent's tree is no longer usable.
                // Its token must observe the same lifetime as its node and UI instance.
                Assert.That(parentToken.IsDisposed, Is.True);
                Assert.That(childToken.IsDisposed, Is.True);
                Assert.Throws<ControlTokenDisposedException>(() => _ = childToken.UI);
                Assert.DoesNotThrow(() => childToken.Dispose());
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void DisposingParent_WhenChildRecoveryThrows_SiblingsAndParentStillRecover()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingRecoveryPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            var recoveryErrors = new List<RecoveryErrorInfo>();
            manager.OnRecoveryError += recoveryErrors.Add;

            try
            {
                var parentToken = manager.CreateWithSorting(name: "Parent").WaitResult();
                var childASlot = DummySlot.Child(parentToken.UI);
                var childAToken = manager.Create<DummyUI>(childASlot, name: "ChildA").WaitResult();
                var childBSlot = DummySlot.Child(parentToken.UI);
                var childBToken = manager.Create<DummyUI>(childBSlot, name: "ChildB").WaitResult();

                plugin.ThrowForTokenID = childAToken.TokenID;

                Assert.DoesNotThrow(() => parentToken.Dispose());

                // Sibling and parent still finish recovery even though ChildA's handler threw.
                CollectionAssert.Contains(plugin.RecoveredTokenIds, childBToken.TokenID);
                CollectionAssert.Contains(plugin.RecoveredTokenIds, parentToken.TokenID);

                // ChildA itself is not left stuck either: AfterRecoverySelf still ran for it
                // (proven by it also being counted as recovered / returned to the pool), so
                // NodeManager does not retain a permanently orphaned node for it.
                CollectionAssert.Contains(plugin.RecoveredTokenIds, childAToken.TokenID);
                Assert.That(provider.RecoveryCount, Is.EqualTo(3));

                Assert.That(canvasProvider.RegisteredIds.Count, Is.EqualTo(0));

                // The error payload carries enough node info (token id, name, phase) to debug which UI failed.
                Assert.That(recoveryErrors.Count, Is.EqualTo(1));
                Assert.That(recoveryErrors[0].TokenID, Is.EqualTo(childAToken.TokenID));
                Assert.That(recoveryErrors[0].NodeName, Does.Contain("ChildA"));
                Assert.That(recoveryErrors[0].Phase, Is.EqualTo(RecoveryPhase.BeforeRecovery));
                Assert.That(recoveryErrors[0].Exception, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void UnexpectedDestruction_ConvergesTreeWithoutNormalRecovery()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var manager = new TestableUIManager(provider, canvasProvider, Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());
            var notifications = new List<UnexpectedDestructionInfo>();
            manager.OnUnexpectedDestruction += notifications.Add;

            try
            {
                var parentToken = manager.CreateWithSorting(name: "Parent").WaitResult();
                var childToken = manager.Create<DummyUI>(DummySlot.Child(parentToken.UI), name: "Child").WaitResult();
                var virtualChildToken = manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(parentToken.UI)).WaitResult();

                manager.HandleUnexpectedDestruction(parentToken);

                Assert.That(provider.RecoveryCount, Is.EqualTo(0));
                Assert.That(provider.UnexpectedRecoveryCount, Is.EqualTo(2));
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));
                Assert.That(canvasProvider.RegisteredIds, Is.Empty);
                Assert.That(parentToken.IsDisposed, Is.True);
                Assert.That(childToken.IsDisposed, Is.True);
                Assert.That(virtualChildToken.IsDisposed, Is.True);
                CollectionAssert.AreEquivalent(new[] { parentToken.TokenID, childToken.TokenID, virtualChildToken.TokenID }, notifications.Select(info => info.TokenID));
                Assert.DoesNotThrow(() => parentToken.Dispose());
                Assert.DoesNotThrow(() => childToken.Dispose());
                Assert.DoesNotThrow(() => virtualChildToken.Dispose());
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void UnexpectedDestruction_WhenPluginThrows_StillCleansUpTheElement()
        {
            var provider = new FakeUIElementProvider();
            var canvasProvider = new FakeCanvasProvider();
            var plugin = new ThrowingUnexpectedDestructionPlugin();
            var manager = new TestableUIManager(provider, canvasProvider, new IPlugin<DummyUI, DummyContainer, object>[] { plugin }, DummyLogger.Create());

            try
            {
                var token = manager.CreateWithSorting().WaitResult();

                Assert.DoesNotThrow(() => manager.HandleUnexpectedDestruction(token));
                Assert.That(provider.UnexpectedRecoveryCount, Is.EqualTo(1));
                Assert.That(canvasProvider.RegisteredIds, Is.Empty);
                Assert.That(token.IsDisposed, Is.True);
            }
            finally
            {
                manager.Dispose();
            }
        }

    }
}
