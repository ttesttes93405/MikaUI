using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MikaUI
{
    public class DefaultUIElementProvider : IUIElementProvider<MonoBehaviour, Transform>
    {
        public interface IUIElementSource
        {
            string UIName { get; }
            MonoBehaviour UITemplate { get; }
        }

        readonly IEnumerable<IUIElementSource> sources;
        readonly IObjectPool<IUIElementSource, IVisualUI> pool;
        readonly Transform poolRoot;
        List<UIElement<Transform>> uiElementList;
        bool isDisposed;

        public DefaultUIElementProvider(IEnumerable<IUIElementSource> sources)
            : this(sources, null, null) { }

        public DefaultUIElementProvider(
            IEnumerable<IUIElementSource> sources,
            IObjectPool<IUIElementSource, IVisualUI> pool,
            Transform poolRoot)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
            this.pool = pool;
            if (pool != null && poolRoot == null)
                throw new ArgumentNullException(nameof(poolRoot));
            this.poolRoot = poolRoot;
        }

        /// <summary>Creates the Core pool with Unity's GameObject disposal behavior.</summary>
        public static BoundedEffectablePool<IUIElementSource, IVisualUI> CreateBoundedPool(
            Func<IUIElementSource, int> capacityForSource)
        {
            return new BoundedEffectablePool<IUIElementSource, IVisualUI>(
                capacityForSource,
                ui =>
                {
                    if (ui is MonoBehaviour component && component != null)
                        UnityEngine.Object.Destroy(component.gameObject);
                });
        }

        public MikaTask<UIElement<Transform>> GetUIElement<T>(string name) where T : MonoBehaviour, IVisualUI
        {
            ThrowIfDisposed();
            uiElementList ??= sources.Select(CreateUIElement).Where(p => p != null).ToList();
            var result = uiElementList.FirstOrDefault(p => p.GetTemplate().GetType() == typeof(T) && p.UIName == name);
            return MikaTask<UIElement<Transform>>.FromResult(result);
        }

        UIElement<Transform> CreateUIElement(IUIElementSource source)
        {
            if (source.UITemplate is not IVisualUI template)
            {
                Debug.LogError($"UIElementProvider: {source.UITemplate.name} is not IVisualUI");
                return null;
            }

            var cleaners = new Dictionary<IVisualUI, Action>();
            var ids = new Dictionary<IVisualUI, Guid>();
            var readyForReuse = new HashSet<IVisualUI>();

            return new UIElement<Transform>
            {
                UIName = source.UIName,
                GetTemplate = () => template,
                Create = container =>
                {
                    ThrowIfDisposed();
                    IVisualUI ui = null;
                    if (pool != null)
                    {
                        while (pool.TryRent(source, out var candidate))
                        {
                            if ((candidate as MonoBehaviour) == null)
                            {
                                cleaners.Remove(candidate);
                                ids.Remove(candidate);
                                readyForReuse.Remove(candidate);
                                continue;
                            }

                            ui = candidate;
                            (ui as MonoBehaviour).transform.SetParent(container, false);
                            break;
                        }
                    }

                    ui ??= UnityEngine.Object.Instantiate(source.UITemplate, container) as IVisualUI;
                    if (ids.TryGetValue(ui, out var id) == false)
                    {
                        id = Guid.NewGuid();
                        ids.Add(ui, id);
                    }

                    return MikaTask<(IVisualUI, Action, Guid)>.FromResult((ui, OnCreated, id));

                    void OnCreated()
                    {
                        if (ui is IUIEffectable effectable)
                            cleaners[ui] = effectable.UseEffect();
                        readyForReuse.Add(ui);
                    }
                },
                Recovery = ui =>
                {
                    if ((ui as MonoBehaviour) == null)
                    {
                        cleaners.Remove(ui);
                        ids.Remove(ui);
                        readyForReuse.Remove(ui);
                        return;
                    }

                    cleaners.TryGetValue(ui, out var cleaner);
                    var cleanupSucceeded = false;
                    try
                    {
                        cleaner?.Invoke();
                        cleanupSucceeded = true;
                    }
                    finally
                    {
                        cleaners.Remove(ui);
                        var activated = readyForReuse.Remove(ui);
                        ReturnOrDestroy(source, ui, ids, activated && cleanupSucceeded);
                    }
                },
                UnexpectedRecovery = ui =>
                {
                    // A destroyed Unity object must never be returned to the pool.
                    if ((ui as MonoBehaviour) == null)
                    {
                        cleaners.Remove(ui);
                        ids.Remove(ui);
                        readyForReuse.Remove(ui);
                        return;
                    }

                    cleaners.TryGetValue(ui, out var cleaner);
                    var cleanupSucceeded = false;
                    try
                    {
                        cleaner?.Invoke();
                        cleanupSucceeded = true;
                    }
                    finally
                    {
                        cleaners.Remove(ui);
                        var activated = readyForReuse.Remove(ui);
                        ReturnOrDestroy(source, ui, ids, activated && cleanupSucceeded);
                    }
                },
            };
        }

        void ReturnOrDestroy(IUIElementSource source, IVisualUI ui, Dictionary<IVisualUI, Guid> ids, bool mayReuse)
        {
            var component = ui as MonoBehaviour;
            if (component == null)
            {
                ids.Remove(ui);
                return;
            }

            var retained = false;
            try
            {
                if (mayReuse && !isDisposed && pool != null && poolRoot != null)
                {
                    component.transform.SetParent(poolRoot, false);
                    retained = pool.TryReturn(source, ui);
                }
            }
            finally
            {
                if (!retained)
                {
                    ids.Remove(ui);
                    UnityEngine.Object.Destroy(component.gameObject);
                }
            }
        }

        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new()
        {
            ThrowIfDisposed();
            return new VirtualUIElement
            {
                UIName = typeof(T).Name,
                Create = () =>
                {
                    ThrowIfDisposed();
                    var virtualUI = new T();
                    var elementId = Guid.NewGuid();
                    return MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>.FromResult((virtualUI, () => { }, elementId));
                },
                Recovery = _ => { },
            };
        }

        void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(DefaultUIElementProvider));
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            try
            {
                pool?.Dispose();
            }
            finally
            {
                uiElementList = null;
            }
        }
    }
}
