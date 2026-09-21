using System;
using System.Collections;
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
        GameObject uiTemplateObject;
        UnityUIManager manager;

        [UnityTest]
        public IEnumerator DestroyingManagedParent_RecoversTokensAndDestroyedPoolEntries()
        {
            CreateManager();

            var parentToken = CreateRoot();
            var parentUI = parentToken.UI;
            var parentElementId = parentToken.ElementID;
            var childToken = CreateChild(parentToken);
            var childUI = childToken.UI;
            var childElementId = childToken.ElementID;

            UnityEngine.Object.Destroy(parentUI.gameObject);
            yield return null;

            Assert.That(parentUI == null, Is.True);
            Assert.That(childUI == null, Is.True);
            Assert.That(parentToken.IsDisposed, Is.True);
            Assert.That(childToken.IsDisposed, Is.True);
            Assert.That(ReusableTestUI.CleanupCount, Is.EqualTo(0));

            var replacementToken = CreateRoot();
            Assert.That(replacementToken.UI == null, Is.False);
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(parentElementId));
            Assert.That(replacementToken.ElementID, Is.Not.EqualTo(childElementId));

            replacementToken.Dispose();
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            manager?.Dispose();
            manager = null;

            DestroyObject(uiTemplateObject);
            DestroyObject(canvasTemplateObject);
            DestroyObject(canvasRootObject);
            DestroyObject(poolRootObject);

            yield return null;
        }

        void CreateManager()
        {
            ReusableTestUI.ResetCounters();
            canvasRootObject = new GameObject("Canvas Root", typeof(RectTransform));
            canvasTemplateObject = new GameObject("Canvas Template", typeof(RectTransform), typeof(Canvas));
            poolRootObject = new GameObject("Pool Root", typeof(RectTransform));
            uiTemplateObject = new GameObject("Reusable UI Template", typeof(RectTransform), typeof(ReusableTestUI));

            var source = new TestUIElementSource("", uiTemplateObject.GetComponent<ReusableTestUI>());
            manager = new UnityUIManager(
                new[] { source },
                poolRootObject.transform,
                canvasRootObject.GetComponent<RectTransform>(),
                canvasTemplateObject.GetComponent<Canvas>(),
                new IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[]
                {
                    new DestroyDetectPlugin(_ => { }),
                });
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

    }

    public sealed class ReusableTestUI : MonoBehaviour, IVisualUI, IUIEffectable
    {
        public static int CleanupCount { get; private set; }

        public static void ResetCounters()
        {
            CleanupCount = 0;
        }

        public Action UseEffect()
        {
            return () => CleanupCount++;
        }
    }
}
