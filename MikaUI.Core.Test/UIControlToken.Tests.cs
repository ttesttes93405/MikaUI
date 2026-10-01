using System;
using System.Collections.Generic;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;

namespace Tests.Core
{
    public class UIControlTokenTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void HashSetMembershipSurvivesEventSubscriptionAndDisposal(bool visual)
        {
            using var manager = CreateManager();
            UIControlToken token = visual
                ? manager.Create<DummyUI>(DummySlot.Root()).WaitResult()
                : manager.CreateVirtual<DummyVirtualUI>().WaitResult();
            var tokens = new HashSet<UIControlToken> { token };
            var hash = token.GetHashCode();
            var disposeCount = 0;
            Action onDispose = () => disposeCount++;

            token.OnDispose += onDispose;
            Assert.That(token.GetHashCode(), Is.EqualTo(hash));
            Assert.That(tokens.Contains(token), Is.True);

            token.Dispose();
            token.Dispose();
            token.OnDispose -= onDispose;

            Assert.That(disposeCount, Is.EqualTo(1));
            Assert.That(token.GetHashCode(), Is.EqualTo(hash));
            Assert.That(tokens.Contains(token), Is.True);
            Assert.That(tokens.Remove(token), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AliasesShareDisposalStateAndRejectUiAccess(bool visual)
        {
            using var manager = CreateManager();
            if (visual)
            {
                var token = manager.Create<DummyUI>(DummySlot.Root()).WaitResult();
                UIControlToken<DummyUI> alias = token;
                Assert.That(alias.UI, Is.SameAs(token.UI));
                VerifyDisposedAlias(token, alias, () => { _ = alias.UI; });
            }
            else
            {
                var token = manager.CreateVirtual<DummyVirtualUI>().WaitResult();
                var alias = token;
                Assert.That(alias.UI, Is.SameAs(token.UI));
                VerifyDisposedAlias(token, alias, () => { _ = alias.UI; });
            }
        }

        static void VerifyDisposedAlias(UIControlToken token, UIControlToken alias, Action readUi)
        {
            Assert.That(token == alias, Is.True);
            token.Dispose();
            Assert.That(alias.IsDisposed, Is.True);
            Assert.Throws<ControlTokenDisposedException>(() => readUi());
            Assert.Throws<ControlTokenDisposedException>(() => { _ = alias.Recovery; });
        }

        static TestableUIManager CreateManager() => new TestableUIManager(
            new FakeUIElementProvider(), new FakeCanvasProvider(),
            Array.Empty<IPlugin<DummyUI, DummyContainer, object>>(), DummyLogger.Create());
    }
}
