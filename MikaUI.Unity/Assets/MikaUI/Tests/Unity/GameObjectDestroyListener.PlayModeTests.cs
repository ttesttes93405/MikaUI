using System;
using System.Collections;
using MikaUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Unity
{
    public class GameObjectDestroyListenerPlayModeTests
    {
        GameObject target;

        [UnityTest]
        public IEnumerator UnsubscribingLastCallback_RemovesListenerWithoutCallingCallbacks()
        {
            target = new GameObject("Destroy listener test");
            var callbackCount = 0;
            Action callback = () => callbackCount++;
            var unsubscribeFirst = GameObjectDestroyListener.AttachTo(1, target, callback);
            var unsubscribeSecond = GameObjectDestroyListener.AttachTo(1, target, callback);
            var unsubscribeOtherManager = GameObjectDestroyListener.AttachTo(2, target, callback);

            unsubscribeFirst();
            unsubscribeFirst();
            Assert.That(target.GetComponent<GameObjectDestroyListener>(), Is.Not.Null);

            unsubscribeSecond();
            Assert.That(target.GetComponent<GameObjectDestroyListener>(), Is.Not.Null);

            unsubscribeOtherManager();
            unsubscribeOtherManager();
            yield return null;

            Assert.That(target.GetComponent<GameObjectDestroyListener>(), Is.Null);
            Assert.That(callbackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UnsubscribingDuringOnDestroy_IsSafeToRepeat()
        {
            target = new GameObject("Destroy listener test");
            Action unsubscribe = null;
            var callbackCount = 0;
            unsubscribe = GameObjectDestroyListener.AttachTo(1, target, () =>
            {
                callbackCount++;
                unsubscribe();
            });

            UnityEngine.Object.Destroy(target);
            yield return null;

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.DoesNotThrow(() => unsubscribe());
        }

        [UnityTest]
        public IEnumerator ResubscribingBeforeRemoval_ComesFromNewListener()
        {
            target = new GameObject("Destroy listener test");
            var oldCallbackCount = 0;
            var newCallbackCount = 0;
            var anotherCallbackCount = 0;
            var unsubscribe = GameObjectDestroyListener.AttachTo(1, target, () => oldCallbackCount++);

            unsubscribe();
            GameObjectDestroyListener.AttachTo(1, target, () => newCallbackCount++);
            GameObjectDestroyListener.AttachTo(1, target, () => anotherCallbackCount++);
            yield return null;

            Assert.That(target.GetComponents<GameObjectDestroyListener>(), Has.Length.EqualTo(1));
            Assert.That(oldCallbackCount, Is.Zero);
            Assert.That(newCallbackCount, Is.Zero);
            Assert.That(anotherCallbackCount, Is.Zero);

            UnityEngine.Object.Destroy(target);
            yield return null;

            Assert.That(oldCallbackCount, Is.Zero);
            Assert.That(newCallbackCount, Is.EqualTo(1));
            Assert.That(anotherCallbackCount, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (target != null)
                UnityEngine.Object.Destroy(target);

            yield return null;
        }
    }
}
