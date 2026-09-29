using System;
using UnityEngine;

namespace MikaUI
{
    public interface IUnityUIElementPool<TSource> : IDisposable where TSource : class
    {
        bool TryRent(TSource source, Transform container, out IVisualUI ui);
        void Release(TSource source, IVisualUI ui, bool canReuse);
    }
}
