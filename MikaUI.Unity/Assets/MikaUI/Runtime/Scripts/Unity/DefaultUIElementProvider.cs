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
        readonly IUnityUIElementPool<IUIElementSource> pool;
        readonly IUIInstanceUseLifecycle<IVisualUI> instanceUseLifecycle;
        List<UIElement<Transform>> uiElementList;
        bool isDisposed;

        public DefaultUIElementProvider(IEnumerable<IUIElementSource> sources)
            : this(sources, new UnityDestroyOnReleasePool<IUIElementSource>()) { }

        public DefaultUIElementProvider(
            IEnumerable<IUIElementSource> sources,
            IUnityUIElementPool<IUIElementSource> pool,
            IUIInstanceUseLifecycle<IVisualUI> instanceUseLifecycle = null)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.instanceUseLifecycle = instanceUseLifecycle ?? new DefaultUIInstanceUseLifecycle<IVisualUI>();
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

            return new UIElement<Transform>
            {
                UIName = source.UIName,
                GetTemplate = () => template,
                Create = container =>
                {
                    ThrowIfDisposed();
                    pool.TryRent(source, container, out var ui);
                    ui ??= UnityEngine.Object.Instantiate(source.UITemplate, container) as IVisualUI;
                    try
                    {
                        var elementId = instanceUseLifecycle.BeginUse(ui);
                        return MikaTask<(IVisualUI, Action, Guid)>.FromResult((ui, () => instanceUseLifecycle.OnCreated(ui), elementId));
                    }
                    catch
                    {
                        pool.Release(source, ui, false);
                        throw;
                    }
                },
                Recovery = ui => Recover(source, ui),
                UnexpectedRecovery = ui => Recover(source, ui),
            };
        }

        void Recover(IUIElementSource source, IVisualUI ui)
        {
            var canReuse = false;
            try
            {
                canReuse = instanceUseLifecycle.Recover(ui, (ui as MonoBehaviour) != null);
            }
            finally
            {
                pool.Release(source, ui, canReuse && !isDisposed);
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
