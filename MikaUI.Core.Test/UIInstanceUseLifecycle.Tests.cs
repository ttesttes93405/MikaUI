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

            public Action UseEffect()
            {
                Starts++;
                return () =>
                {
                    Cleanups++;
                    if (ThrowOnCleanup)
                        throw new InvalidOperationException("Cleanup failed");
                };
            }
        }

        sealed class PlainUI : IVisualUI { }

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
