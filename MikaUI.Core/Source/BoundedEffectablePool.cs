using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MikaUI
{
    /// <summary>
    /// A FIFO pool with a fixed idle capacity per source. Only IUIEffectable
    /// instances are retained. The supplied discard action releases idle items
    /// when the pool is disposed; the caller destroys items rejected by TryReturn.
    /// </summary>
    public sealed class BoundedEffectablePool<TSource, TUI> : IObjectPool<TSource, TUI>
        where TSource : class
        where TUI : class, IVisualUI
    {
        sealed class Bucket
        {
            public readonly int Capacity;
            public readonly Queue<TUI> Items = new();

            public Bucket(int capacity) => Capacity = capacity;
        }

        sealed class SourceIdentityComparer : IEqualityComparer<TSource>
        {
            public bool Equals(TSource x, TSource y) => ReferenceEquals(x, y);
            public int GetHashCode(TSource source) => RuntimeHelpers.GetHashCode(source);
        }

        readonly Func<TSource, int> capacityForSource;
        readonly Action<TUI> discard;
        readonly Dictionary<TSource, Bucket> buckets;
        bool isDisposed;

        public BoundedEffectablePool(
            Func<TSource, int> capacityForSource,
            Action<TUI> discard,
            IEqualityComparer<TSource> sourceComparer = null)
        {
            this.capacityForSource = capacityForSource ?? throw new ArgumentNullException(nameof(capacityForSource));
            this.discard = discard ?? throw new ArgumentNullException(nameof(discard));
            buckets = new Dictionary<TSource, Bucket>(sourceComparer ?? new SourceIdentityComparer());
        }

        public bool TryRent(TSource source, out TUI ui)
        {
            ThrowIfDisposed();
            var bucket = GetBucket(source);
            if (bucket.Items.Count > 0)
            {
                ui = bucket.Items.Dequeue();
                return true;
            }

            ui = null;
            return false;
        }

        public bool TryReturn(TSource source, TUI ui)
        {
            if (isDisposed)
                return false;
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

            var bucket = GetBucket(source);
            if (ui is not IUIEffectable || bucket.Items.Count >= bucket.Capacity)
                return false;

            bucket.Items.Enqueue(ui);
            return true;
        }

        Bucket GetBucket(TSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (buckets.TryGetValue(source, out var bucket))
                return bucket;

            var capacity = capacityForSource(source);
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityForSource), capacity, "Pool capacity cannot be negative.");

            bucket = new Bucket(capacity);
            buckets.Add(source, bucket);
            return bucket;
        }

        void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            List<Exception> errors = null;
            foreach (var bucket in buckets.Values)
            {
                while (bucket.Items.Count > 0)
                {
                    try
                    {
                        discard(bucket.Items.Dequeue());
                    }
                    catch (Exception error)
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }
            }
            buckets.Clear();

            if (errors != null)
                throw new AggregateException("Failed to discard one or more pooled UI instances.", errors);
        }
    }
}
