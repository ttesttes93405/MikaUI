using UnityEngine;

namespace MikaUI
{
    /// <summary>Uses the pool release contract without retaining idle instances.</summary>
    public sealed class UnityDestroyOnReleasePool<TSource> : IUnityUIElementPool<TSource>
        where TSource : class
    {
        public bool TryRent(TSource source, Transform container, out IVisualUI ui)
        {
            ui = null;
            return false;
        }

        public void Release(TSource source, IVisualUI ui, bool canReuse)
        {
            if (ui is MonoBehaviour component && component != null)
                UnityEngine.Object.Destroy(component.gameObject);
        }

        public void Dispose() { }
    }
}
