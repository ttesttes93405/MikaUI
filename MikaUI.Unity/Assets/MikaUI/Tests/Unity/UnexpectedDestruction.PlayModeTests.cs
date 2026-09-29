using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Unity
{
    public class UnexpectedDestructionPlayModeTests
    {
        GameObject canvasRootObject;
        GameObject canvasTemplateObject;
        GameObject poolRootObject;
        GameObject externalContainerObject;
        GameObject uiTemplateObject;
        UnityUIManager manager;

        [UnityTest]
        public IEnumerator DestroyingManagedParent_RecoversTokensAndDestroyedPoolEntries()
        {
            CreateManager();

            var parentToken = CreateRoot();
            var parentUI = parentToken.UI;
            var canvas = parentUI.transform.parent.GetComponent<Canvas>();
            var parentElementId = parentToken.ElementID;
            var childToken = CreateChild(parentToken);
            var childUI = childToken.UI;
            var childElementId = childToken.ElementID;

            UnityEngine.Object.Destroy(parentUI.gameObject);
            yield return WaitUntilDisposed(parentToken);

            Assert.That(parentUI == null, Is.True);
            Assert.That(childUI == null, Is.True);
            Assert.That(parentToken.IsDisposed, Is.True);
            Assert.That(childToken.IsDisposed, Is.True);
            Assert.That(ReusableTestUI.CleanupCount, Is.EqualTo(0));
            Assert.That(canvas.enabled, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.False);

            var replacementToken = CreateRoot();
            Assert.That(replacementToken.UI == null, Is.False);
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(parentElementId));
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(childElementId));

            replacementToken.Dispose();
        }

        [UnityTest]
        public IEnumerator DisposingTokenInCreatedPlugin_RecoversCanvasWithoutRunningEffect()
        {
            var disposingPlugin = new DisposeFirstCreatedTokenPlugin();
            CreateManager(disposingPlugin);

            LogAssert.Expect(LogType.Error, new Regex("ControlTokenDisposedException"));
            var failedCreate = manager.Create<ReusableTestUI>(sortingOrder: 0);
            Assert.That(failedCreate.IsCompleted, Is.True);
            Assert.That(failedCreate.IsFaulted, Is.True);
            Assert.That(failedCreate.Exception.InnerException, Is.TypeOf<ControlTokenDisposedException>());
            Assert.That(disposingPlugin.DisposedToken.IsDisposed, Is.True);
            Assert.That(ReusableTestUI.EffectCount, Is.Zero);

            var canvas = canvasRootObject.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.enabled, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.False);

            // An unused canvas can be reassigned to a different sorting order.
            var nextToken = WaitFor(manager.Create<ReusableTestUI>(sortingOrder: 1));
            Assert.That(nextToken.UI.transform.parent.GetComponent<Canvas>(), Is.SameAs(canvas));
            nextToken.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreatingAnotherOrderInCreatedPlugin_KeepsPendingCanvasReserved()
        {
            var creatingPlugin = new CreateOtherOrderInCreatedPlugin();
            CreateManager(creatingPlugin);

            var firstToken = WaitFor(manager.Create<ReusableTestUI>(sortingOrder: 0));
            var secondToken = creatingPlugin.SecondToken;
            Assert.That(secondToken, Is.Not.Null);

            var firstCanvas = firstToken.UI.transform.parent.GetComponent<Canvas>();
            var secondCanvas = secondToken.UI.transform.parent.GetComponent<Canvas>();
            Assert.That(secondCanvas, Is.Not.SameAs(firstCanvas));
            Assert.That(firstCanvas.sortingOrder, Is.EqualTo(0));
            Assert.That(secondCanvas.sortingOrder, Is.EqualTo(1));

            firstToken.Dispose();
            Assert.That(firstCanvas.gameObject.activeSelf, Is.False);
            Assert.That(secondCanvas.gameObject.activeSelf, Is.True);

            secondToken.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposingRegisteredToken_DoesNotRecoverCanvasWithPendingCreation()
        {
            var creatingPlugin = new CreateAndDisposeSameOrderInCreatedPlugin();
            CreateManager(creatingPlugin);

            var pendingToken = WaitFor(manager.Create<ReusableTestUI>(sortingOrder: 0));
            Assert.That(creatingPlugin.CanvasStayedActive, Is.True);
            Assert.That(pendingToken.UI.transform.parent.GetComponent<Canvas>().gameObject.activeSelf, Is.True);

            pendingToken.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingPooledUi_DiscardsItBeforeTheNextCreate()
        {
            CreateManager();

            var firstToken = CreateRoot();
            var firstUI = firstToken.UI;
            var firstElementId = firstToken.ElementID;
            firstToken.Dispose();

            UnityEngine.Object.Destroy(firstUI.gameObject);
            yield return null;

            var replacementToken = CreateRoot();
            Assert.That(replacementToken.UI == null, Is.False);
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(firstElementId));

            replacementToken.Dispose();
        }

        [UnityTest]
        public IEnumerator WithoutPool_RecoversByDestroyingAndStillRunsCleanup()
        {
            CreateManager(poolCapacity: null);

            var firstToken = CreateRoot();
            var firstUI = firstToken.UI;
            var firstElementId = firstToken.ElementID;
            firstToken.Dispose();
            yield return null;

            Assert.That(firstUI == null, Is.True);
            Assert.That(ReusableTestUI.CleanupCount, Is.EqualTo(1));

            var replacementToken = CreateRoot();
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(firstElementId));
            replacementToken.Dispose();
        }

        [UnityTest]
        public IEnumerator PoolCapacityOne_RetainsOnlyTheFirstReturnedInstance()
        {
            CreateManager(poolCapacity: 1);

            var firstToken = CreateRoot();
            var firstUI = firstToken.UI;
            var firstElementId = firstToken.ElementID;
            var secondToken = CreateRoot();
            var secondUI = secondToken.UI;
            firstToken.Dispose();
            secondToken.Dispose();
            yield return null;

            Assert.That(firstUI == null, Is.False);
            Assert.That(secondUI == null, Is.True);
            var replacementToken = CreateRoot();
            Assert.That(replacementToken.UI, Is.SameAs(firstUI));
            Assert.That(replacementToken.ElementID, Is.EqualTo(firstElementId));
            replacementToken.Dispose();
        }

        [UnityTest]
        public IEnumerator ManagerDispose_DestroysIdlePooledInstances()
        {
            CreateManager(poolCapacity: 1);

            var token = CreateRoot();
            var ui = token.UI;
            token.Dispose();
            Assert.That(ui == null, Is.False);

            manager.Dispose();
            yield return null;
            Assert.That(ui == null, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingManagedParent_RecoversExternalChildNormally()
        {
            CreateManager();

            var parentToken = CreateRoot();
            var parentUI = parentToken.UI;
            var childToken = CreateChildInExternalContainer(parentToken);
            var childUI = childToken.UI;
            var childElementId = childToken.ElementID;

            UnityEngine.Object.Destroy(parentUI.gameObject);
            yield return WaitUntilDisposed(parentToken);

            Assert.That(parentUI == null, Is.True);
            Assert.That(childUI == null, Is.False);
            Assert.That(parentToken.IsDisposed, Is.True);
            Assert.That(childToken.IsDisposed, Is.True);
            Assert.That(ReusableTestUI.CleanupCount, Is.EqualTo(1));
            Assert.That(childUI.transform.parent, Is.EqualTo(poolRootObject.transform));

            var replacementToken = CreateRoot();
            Assert.That(replacementToken.UI, Is.SameAs(childUI));
            Assert.That(replacementToken.ElementID, Is.EqualTo(childElementId));

            replacementToken.Dispose();
        }

        [UnityTest]
        public IEnumerator DestroyingRootContainerBeforeManagerDispose_ReportsOwnershipViolation()
        {
            CreateManager();
            CreateRoot();

            LogAssert.Expect(
                LogType.Exception,
                new Regex("RootContainer was destroyed before its UIManager was disposed"));

            UnityEngine.Object.Destroy(canvasRootObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            manager?.Dispose();
            manager = null;

            DestroyObject(uiTemplateObject);
            DestroyObject(canvasTemplateObject);
            DestroyObject(canvasRootObject);
            DestroyObject(poolRootObject);
            DestroyObject(externalContainerObject);

            yield return null;
        }

        void CreateManager(IPlugin<MonoBehaviour, Transform, SlotRectConfigs> additionalPlugin = null, int? poolCapacity = 8)
        {
            ReusableTestUI.ResetCounters();
            canvasRootObject = new GameObject("Canvas Root", typeof(RectTransform));
            canvasTemplateObject = new GameObject("Canvas Template", typeof(RectTransform), typeof(Canvas));
            poolRootObject = new GameObject("Pool Root", typeof(RectTransform));
            externalContainerObject = new GameObject("External Container", typeof(RectTransform));
            uiTemplateObject = new GameObject("Reusable UI Template", typeof(RectTransform), typeof(ReusableTestUI));

            var source = new TestUIElementSource("", uiTemplateObject.GetComponent<ReusableTestUI>());
            var canvasProvider = new CanvasProvider(
                canvasRootObject.GetComponent<RectTransform>(),
                canvasTemplateObject.GetComponent<Canvas>());
            var destroyDetectPlugin = new DestroyDetectPlugin(canvasProvider, _ => { });
            var plugins = additionalPlugin == null
                ? new IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[] { destroyDetectPlugin }
                : new IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[] { additionalPlugin, destroyDetectPlugin };
            var provider = poolCapacity.HasValue
                ? new DefaultUIElementProvider(
                    new[] { source },
                    new UnityBoundedEffectablePool<DefaultUIElementProvider.IUIElementSource>(
                        _ => poolCapacity.Value, poolRootObject.transform))
                : new DefaultUIElementProvider(new[] { source }, pool: null);
            manager = new UnityUIManager(
                provider,
                canvasProvider,
                plugins);
        }

        UIControlToken<ReusableTestUI, Transform> CreateRoot()
        {
            return WaitFor(manager.Create<ReusableTestUI>(sortingOrder: 0));
        }

        UIControlToken<ReusableTestUI, Transform> CreateChild(UIControlToken<ReusableTestUI, Transform> parentToken)
        {
            var slot = new VisualSlot(parentToken.UI, parentToken.UI.transform);
            return WaitFor(manager.Create<ReusableTestUI>(slot).ToTask());
        }

        UIControlToken<ReusableTestUI, Transform> CreateChildInExternalContainer(UIControlToken<ReusableTestUI, Transform> parentToken)
        {
            var slot = new VisualSlot(parentToken.UI, externalContainerObject.transform);
            return WaitFor(manager.Create<ReusableTestUI>(slot).ToTask());
        }

        static T WaitFor<T>(Task<T> task)
        {
            Assert.That(task.IsCompleted, Is.True, "The default test provider completes synchronously.");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            return task.GetAwaiter().GetResult();
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }
        }

        static IEnumerator WaitUntilDisposed(UIControlToken token, int maxFrames = 10)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (token.IsDisposed)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Token {token.TokenID} was not disposed within {maxFrames} frames.");
        }

        sealed class TestUIElementSource : DefaultUIElementProvider.IUIElementSource
        {
            public string UIName { get; }
            public MonoBehaviour UITemplate { get; }

            public TestUIElementSource(string uiName, MonoBehaviour uiTemplate)
            {
                UIName = uiName;
                UITemplate = uiTemplate;
            }
        }

        sealed class DisposeFirstCreatedTokenPlugin :
            IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
            IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>
        {
            public int SortingOrder => 0;
            public UIControlToken DisposedToken { get; private set; }

            public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager) { }
            public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager) { }

            public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
            {
                if (DisposedToken != null)
                    return;

                DisposedToken = token;
                token.Dispose();
            }
        }

        sealed class CreateOtherOrderInCreatedPlugin :
            IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
            IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>
        {
            UnityUIManager manager;
            bool hasStartedSecondCreate;

            public int SortingOrder => 0;
            public UIControlToken<ReusableTestUI, Transform> SecondToken { get; private set; }

            public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
            {
                this.manager = (UnityUIManager)manager;
            }

            public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager) { }

            public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
            {
                if (hasStartedSecondCreate)
                    return;

                hasStartedSecondCreate = true;
                SecondToken = manager.Create<ReusableTestUI>(sortingOrder: 1).GetAwaiter().GetResult();
            }
        }

        sealed class CreateAndDisposeSameOrderInCreatedPlugin :
            IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
            IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>
        {
            UnityUIManager manager;
            bool hasStartedSecondCreate;

            public int SortingOrder => 0;
            public bool CanvasStayedActive { get; private set; }

            public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
            {
                this.manager = (UnityUIManager)manager;
            }

            public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager) { }

            public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
            {
                if (hasStartedSecondCreate)
                    return;

                hasStartedSecondCreate = true;
                var secondToken = manager.Create<ReusableTestUI>(sortingOrder: 0).GetAwaiter().GetResult();
                secondToken.Dispose();
                CanvasStayedActive = container.GetComponent<Canvas>().gameObject.activeSelf;
            }
        }

    }

    public sealed class ReusableTestUI : MonoBehaviour, IVisualUI, IUIEffectable
    {
        public static int CleanupCount { get; private set; }
        public static int EffectCount { get; private set; }

        public static void ResetCounters()
        {
            CleanupCount = 0;
            EffectCount = 0;
        }

        public Action UseEffect()
        {
            EffectCount++;
            return () => CleanupCount++;
        }
    }
}
