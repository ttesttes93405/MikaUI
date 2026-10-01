using System;
using MikaUI;
using NUnit.Framework;

namespace Tests.Core
{
    public class UIInstanceUseLifecycleTests
    {
        sealed class EffectUI : IUIEffectable
        {
            public int Starts;
            public int Cleanups;
            public bool ThrowOnCleanup;
            public Action OnSetup;

            public Action UseEffect()
            {
                Starts++;
                OnSetup?.Invoke();
                return () =>
                {
                    Cleanups++;
                    if (ThrowOnCleanup)
                        throw new InvalidOperationException("Cleanup failed");
                };
            }
        }

        sealed class PlainUI : IVisualUI { }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void RecoveryDuringSetup_CompletesCleanupAccordingToLiveness(bool isAlive, int cleanups)
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI();
            var oldId = lifecycle.BeginUse(ui);
            ui.OnSetup = () =>
            {
                Assert.That(lifecycle.Recover(ui, isAlive), Is.False);
                Assert.That(lifecycle.Recover(ui, isAlive), Is.False);
                Assert.That(ui.Cleanups, Is.Zero);
            };

            lifecycle.OnCreated(ui);

            Assert.That(ui.Cleanups, Is.EqualTo(cleanups));
            Assert.That(lifecycle.Recover(ui, true), Is.False);
            Assert.That(lifecycle.BeginUse(ui), Is.Not.EqualTo(oldId));
        }

        [Test]
        public void RecoveryDuringSetup_DoesNotOverwriteANewUse()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI();
            lifecycle.BeginUse(ui);
            Guid nextId = default;
            ui.OnSetup = () =>
            {
                Assert.That(lifecycle.Recover(ui, true), Is.False);
                ui.OnSetup = null;
                nextId = lifecycle.BeginUse(ui);
                lifecycle.OnCreated(ui);
            };

            lifecycle.OnCreated(ui);

            Assert.That(ui.Cleanups, Is.EqualTo(1));
            Assert.That(lifecycle.Recover(ui, true), Is.True);
            Assert.That(ui.Cleanups, Is.EqualTo(2));
            Assert.That(lifecycle.BeginUse(ui), Is.EqualTo(nextId));
        }

        [Test]
        public void RecoveryDuringSetup_PropagatesLateCleanupFailureOnce()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI { ThrowOnCleanup = true };
            var oldId = lifecycle.BeginUse(ui);
            ui.OnSetup = () => lifecycle.Recover(ui, true);

            Assert.Throws<InvalidOperationException>(() => lifecycle.OnCreated(ui));
            Assert.That(ui.Cleanups, Is.EqualTo(1));
            Assert.That(lifecycle.Recover(ui, true), Is.False);
            Assert.That(lifecycle.BeginUse(ui), Is.Not.EqualTo(oldId));
        }

        [Test]
        public void SetupCannotReenterOnCreated()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI();
            lifecycle.BeginUse(ui);
            ui.OnSetup = () => Assert.Throws<InvalidOperationException>(() => lifecycle.OnCreated(ui));

            lifecycle.OnCreated(ui);

            Assert.That(ui.Starts, Is.EqualTo(1));
            Assert.That(lifecycle.Recover(ui, true), Is.True);
            Assert.That(ui.Cleanups, Is.EqualTo(1));
        }

        [Test]
        public void ReusePreservesIdAndRunsCleanupOncePerUse()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI();
            var id = lifecycle.BeginUse(ui);
            lifecycle.OnCreated(ui);
            Assert.That(lifecycle.Recover(ui, true), Is.True);

            Assert.That(lifecycle.BeginUse(ui), Is.EqualTo(id));
            lifecycle.OnCreated(ui);
            Assert.That(lifecycle.Recover(ui, true), Is.True);
            Assert.That(ui.Starts, Is.EqualTo(2));
            Assert.That(ui.Cleanups, Is.EqualTo(2));
        }

        [Test]
        public void IncompleteOrDestroyedUseCannotBeReused()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var incomplete = new EffectUI();
            lifecycle.BeginUse(incomplete);
            Assert.That(lifecycle.Recover(incomplete, true), Is.False);

            var plain = new PlainUI();
            lifecycle.BeginUse(plain);
            lifecycle.OnCreated(plain);
            Assert.That(lifecycle.Recover(plain, true), Is.False);

            var destroyed = new EffectUI();
            var oldId = lifecycle.BeginUse(destroyed);
            lifecycle.OnCreated(destroyed);
            Assert.That(lifecycle.Recover(destroyed, false), Is.False);
            Assert.That(destroyed.Cleanups, Is.Zero);
            Assert.That(lifecycle.BeginUse(destroyed), Is.Not.EqualTo(oldId));
        }

        [Test]
        public void CleanupFailurePreventsReuseAndClearsUseState()
        {
            var lifecycle = new DefaultUIInstanceUseLifecycle<IVisualUI>();
            var ui = new EffectUI { ThrowOnCleanup = true };
            var oldId = lifecycle.BeginUse(ui);
            lifecycle.OnCreated(ui);

            Assert.Throws<InvalidOperationException>(() => lifecycle.Recover(ui, true));
            Assert.That(ui.Cleanups, Is.EqualTo(1));
            Assert.That(lifecycle.BeginUse(ui), Is.Not.EqualTo(oldId));
        }
    }
}
