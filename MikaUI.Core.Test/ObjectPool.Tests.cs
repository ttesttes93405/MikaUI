using System;
using System.Collections.Generic;
using MikaUI;
using NUnit.Framework;

namespace Tests.Core
{
    public class ObjectPoolTests
    {
        sealed class Source { }
        sealed class ReusableUI : IUIEffectable
        {
            public Action UseEffect() => null;
        }
        sealed class PlainUI : IVisualUI { }

        [Test]
        public void CapacityIsPerSourceAndEvaluatedOnce()
        {
            var firstSource = new Source();
            var secondSource = new Source();
            var evaluations = 0;
            var discarded = new List<IVisualUI>();
            var pool = new BoundedEffectablePool<Source, IVisualUI>(
                _ => { evaluations++; return 1; },
                discarded.Add);

            var first = new ReusableUI();
            var overflow = new ReusableUI();
            var second = new ReusableUI();
            Assert.That(pool.TryReturn(firstSource, first), Is.True);
            Assert.That(pool.TryReturn(firstSource, overflow), Is.False);
            Assert.That(pool.TryReturn(secondSource, second), Is.True);

            Assert.That(pool.TryRent(firstSource, out var rentedFirst), Is.True);
            Assert.That(rentedFirst, Is.SameAs(first));
            Assert.That(pool.TryRent(secondSource, out var rentedSecond), Is.True);
            Assert.That(rentedSecond, Is.SameAs(second));
            Assert.That(evaluations, Is.EqualTo(2));
            pool.Dispose();
            Assert.That(discarded, Is.Empty);
        }

        [Test]
        public void RejectsNonEffectableAndZeroCapacity()
        {
            var source = new Source();
            using var pool = new BoundedEffectablePool<Source, IVisualUI>(static _ => 0, _ => { });

            Assert.That(pool.TryReturn(source, new ReusableUI()), Is.False);
            Assert.That(pool.TryReturn(source, new PlainUI()), Is.False);
            Assert.That(pool.TryRent(source, out _), Is.False);
        }

        [Test]
        public void RentsInFifoOrderAndDiscardsIdleItemsOnDispose()
        {
            var source = new Source();
            var discarded = new List<IVisualUI>();
            var pool = new BoundedEffectablePool<Source, IVisualUI>(static _ => 2, discarded.Add);
            var first = new ReusableUI();
            var second = new ReusableUI();

            Assert.That(pool.TryReturn(source, first), Is.True);
            Assert.That(pool.TryReturn(source, second), Is.True);
            Assert.That(pool.TryRent(source, out var rented), Is.True);
            Assert.That(rented, Is.SameAs(first));

            pool.Dispose();
            CollectionAssert.AreEqual(new[] { second }, discarded);
            Assert.That(pool.TryReturn(source, first), Is.False);
            Assert.Throws<ObjectDisposedException>(() => pool.TryRent(source, out _));
            Assert.DoesNotThrow(() => pool.Dispose());
        }

        [Test]
        public void NegativeCapacityFailsOnFirstUse()
        {
            using var pool = new BoundedEffectablePool<Source, IVisualUI>(static _ => -1, _ => { });
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryRent(new Source(), out _));
        }
    }
}
