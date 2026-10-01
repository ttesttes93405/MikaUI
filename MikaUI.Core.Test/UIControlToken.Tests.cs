using System;
using System.Collections.Generic;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;

namespace Tests.Core
{
    public class UIControlTokenTests
    {
        [TestCase(false, "direct")]
        [TestCase(true, "direct")]
        [TestCase(false, "parent")]
        [TestCase(true, "parent")]
        [TestCase(false, "unexpected")]
        [TestCase(true, "unexpected")]
        public void ThrowingDisposeCallbacksDoNotSkipCleanup(bool visual, string recoveryMode)
        {
            using var manager = CreateManager();
            var parent = manager.Create<DummyUI>(DummySlot.Root()).WaitResult();
            UIControlToken token = visual
                ? manager.Create<DummyUI>(DummySlot.Child(parent.UI)).WaitResult()
                : manager.CreateVirtual<DummyVirtualUI>(new VirtualSlot(parent.UI)).WaitResult();
            var registeredTokens = new HashSet<Guid> { token.TokenID };
            var calls = new List<int>();
            var errors = new List<RecoveryErrorInfo>();
            var firstError = new InvalidOperationException("First observer failed");
            var secondError = new ArgumentException("Second observer failed");
            manager.OnRecoveryError += errors.Add;
            token.OnDispose += () =>
            {
                calls.Add(1);
                Assert.That(token.IsDisposed, Is.True);
                token.Dispose(); // Reentrant disposal must not notify again.
                throw firstError;
            };
            token.OnDispose += () => { calls.Add(2); throw secondError; };
            token.OnDispose += () => { calls.Add(3); registeredTokens.Remove(token.TokenID); };

            Assert.DoesNotThrow(() =>
            {
                if (recoveryMode == "parent")
                    parent.Dispose();
                else if (recoveryMode == "unexpected")
                    manager.HandleUnexpectedDestruction(parent);
                else
                    token.Dispose();
            });
            token.Dispose();

            Assert.That(token.IsDisposed, Is.True);
            Assert.That(registeredTokens, Is.Empty);
            Assert.That(calls, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0].TokenID, Is.EqualTo(token.TokenID));
            var aggregate = errors[0].Exception as AggregateException;
            Assert.That(aggregate, Is.Not.Null);
            Assert.That(aggregate.InnerExceptions, Is.EqualTo(new Exception[] { firstError, secondError }));
        }

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
