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
        public void Create_WhenProviderReturnsWrongVisualType_RecoversReturnedInstance()
        {
            var returnedUI = new OtherVisualUI();
            var provider = new FakeUIElementProvider { VisualResultOverride = returnedUI };
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.Create<DummyUI>(DummySlot.Root()).WaitResult());

                Assert.That(exception.InnerExceptions[0], Is.TypeOf<NullReferenceException>());
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
                Assert.That(provider.RecoveredVisualUI, Is.SameAs(returnedUI));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void CreateVirtual_WhenProviderReturnsWrongType_RecoversReturnedInstance()
        {
            var returnedUI = new OtherVirtualUI();
            var provider = new FakeUIElementProvider { VirtualResultOverride = returnedUI };
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(), Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.CreateVirtual<DummyVirtualUI>().WaitResult());

                Assert.That(exception.InnerExceptions[0].Message, Does.Contain("Cannot create UI DummyVirtualUI"));
                Assert.That(provider.VirtualRecoveryCount, Is.EqualTo(1));
                Assert.That(provider.RecoveredVirtualUI, Is.SameAs(returnedUI));
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
                Assert.That(provider.DisposeCount, Is.EqualTo(1));
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
                Assert.That(provider.DisposeCount, Is.EqualTo(1));
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
        public void Create_WhenPluginDisposesToken_StopsCreationAndDoesNotRegisterCanvas()
        {
            var onCreatedCount = 0;
            var provider = new FakeUIElementProvider { OnVisualCreated = () => onCreatedCount++ };
            var canvasProvider = new FakeCanvasProvider();
            var disposingPlugin = new DisposingTokenCreatePlugin();
            var laterPlugin = new RecordingPlugin();
            var manager = new TestableUIManager(provider, canvasProvider,
                new IPlugin<DummyUI, DummyContainer, object>[] { disposingPlugin, laterPlugin }, DummyLogger.Create());

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.CreateWithSorting().WaitResult());

                Assert.That(exception.InnerExceptions[0], Is.TypeOf<ControlTokenDisposedException>());
                Assert.That(disposingPlugin.DisposedToken.IsDisposed, Is.True);
                Assert.That(onCreatedCount, Is.Zero);
                Assert.That(laterPlugin.Events, Does.Not.Contain(EventType.Created));
                Assert.That(canvasProvider.RegisteredIds, Is.Empty);
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));

                manager.Dispose();
                Assert.That(provider.RecoveryCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void CreateVirtual_WhenPluginDisposesToken_StopsCreation()
        {
            var onCreatedCount = 0;
            var provider = new FakeUIElementProvider { OnVirtualCreated = () => onCreatedCount++ };
            var disposingPlugin = new DisposingTokenCreatePlugin();
            var laterPlugin = new RecordingPlugin();
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(),
                new IPlugin<DummyUI, DummyContainer, object>[] { disposingPlugin, laterPlugin }, DummyLogger.Create());

            try
            {
                var exception = Assert.Throws<AggregateException>(() => manager.CreateVirtual<DummyVirtualUI>().WaitResult());

                Assert.That(exception.InnerExceptions[0], Is.TypeOf<ControlTokenDisposedException>());
                Assert.That(disposingPlugin.DisposedToken.IsDisposed, Is.True);
                Assert.That(onCreatedCount, Is.Zero);
                Assert.That(laterPlugin.Events, Does.Not.Contain(EventType.VirtualCreated));
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
        public void Create_InjectsManagerOnlyIntoManagerTypedField()
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
                Assert.That(token.UI.Disposable, Is.Null);
                Assert.That(token.UI.Anything, Is.Null);
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
        public void Constructor_WhenSecondPluginInstallFails_UninstallsOnlyCompletedPlugins()
        {
            var provider = new FakeUIElementProvider();
            var events = new List<string>();
            var first = new InstallLifecyclePlugin("first", 0, events);
            var second = new InstallLifecyclePlugin("second", 1, events)
            {
                InstallException = new InvalidOperationException("Simulated install failure.")
            };
            var third = new InstallLifecyclePlugin("third", 2, events);

            var exception = Assert.Throws<InvalidOperationException>(() => new TestableUIManager(
                provider,
                new FakeCanvasProvider(),
                new IPlugin<DummyUI, DummyContainer, object>[] { third, second, first },
                DummyLogger.Create()));

            Assert.That(exception, Is.SameAs(second.InstallException));
            CollectionAssert.AreEqual(new[] { "Install first", "Install second", "Uninstall first" }, events);
            Assert.That(provider.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_WhenPluginEnumerationFails_DisposesProvider()
        {
            var provider = new FakeUIElementProvider();
            var failure = new InvalidOperationException("Plugin enumeration failed.");

            var exception = Assert.Throws<InvalidOperationException>(() => new TestableUIManager(
                provider,
                new FakeCanvasProvider(),
                ThrowingPlugins(),
                DummyLogger.Create()));
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(provider.DisposeCount, Is.EqualTo(1));

            IEnumerable<IPlugin<DummyUI, DummyContainer, object>> ThrowingPlugins()
            {
                yield return new InstallLifecyclePlugin("first", 0, new List<string>());
                throw failure;
            }
        }

        [Test]
        public void Dispose_RecoversActiveUiBeforeDisposingProvider()
        {
            var provider = new FakeUIElementProvider();
            var manager = new TestableUIManager(provider, new FakeCanvasProvider(),
                Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());
            var token = manager.Create<DummyUI>(DummySlot.Root()).WaitResult();

            manager.Dispose();

            Assert.That(token.IsDisposed, Is.True);
            Assert.That(provider.RecoveryCount, Is.EqualTo(1));
            Assert.That(provider.DisposeCount, Is.EqualTo(1));
            manager.Dispose();
            Assert.That(provider.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_WhenRollbackUninstallFails_PreservesInstallFailureAndContinuesRollback()
        {
            var events = new List<string>();
            var first = new InstallLifecyclePlugin("first", 0, events);
            var second = new InstallLifecyclePlugin("second", 1, events)
            {
                UninstallException = new InvalidOperationException("Simulated uninstall failure.")
            };
            var third = new InstallLifecyclePlugin("third", 2, events)
            {
                InstallException = new InvalidOperationException("Simulated install failure.")
            };

            var exception = Assert.Throws<InvalidOperationException>(() => new TestableUIManager(
                new FakeUIElementProvider(),
                new FakeCanvasProvider(),
                new IPlugin<DummyUI, DummyContainer, object>[] { first, second, third },
                DummyLogger.Create()));

            Assert.That(exception, Is.SameAs(third.InstallException));
            CollectionAssert.AreEqual(new[]
            {
                "Install first", "Install second", "Install third", "Uninstall second", "Uninstall first"
            }, events);
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
