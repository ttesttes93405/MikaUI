using System;
using UnityEngine;

namespace MikaUI
{
    /// <summary>Unity storage and disposal for Core's bounded effectable pool.</summary>
    public sealed class UnityBoundedEffectablePool<TSource> : IUnityUIElementPool<TSource>
        where TSource : class
    {
        readonly BoundedEffectablePool<TSource, IVisualUI> storage;
        readonly Transform poolRoot;

        public UnityBoundedEffectablePool(Func<TSource, int> capacityForSource, Transform poolRoot)
        {
            if (poolRoot == null)
                throw new ArgumentNullException(nameof(poolRoot));

            this.poolRoot = poolRoot;
            storage = new BoundedEffectablePool<TSource, IVisualUI>(capacityForSource, Destroy);
        }

        public bool TryRent(TSource source, Transform container, out IVisualUI ui)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            while (storage.TryRent(source, out var candidate))
            {
                if (candidate is MonoBehaviour component && component != null)
                {
                    component.transform.SetParent(container, false);
                    ui = candidate;
                    return true;
                }
            }

            ui = null;
            return false;
        }

        public void Release(TSource source, IVisualUI ui, bool canReuse)
        {
            if (ui is not MonoBehaviour component || component == null)
                return;

            var retained = false;
            try
            {
                if (canReuse && poolRoot != null)
                {
                    component.transform.SetParent(poolRoot, false);
                    retained = storage.TryReturn(source, ui);
                }
            }
            finally
            {
                if (!retained)
                    Destroy(ui);
            }
        }

        public void Dispose() => storage.Dispose();

        static void Destroy(IVisualUI ui)
        {
            if (ui is MonoBehaviour component && component != null)
                UnityEngine.Object.Destroy(component.gameObject);
        }
    }

}
