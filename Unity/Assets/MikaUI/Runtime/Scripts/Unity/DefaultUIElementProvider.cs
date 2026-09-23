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


        readonly Transform poolRoot;
        readonly IEnumerable<IUIElementSource> sources;
        public DefaultUIElementProvider(IEnumerable<IUIElementSource> sources, Transform poolRoot)
        {
            this.poolRoot = poolRoot;
            this.sources = sources;
        }


        List<UIElement<Transform>> uiElementList = null;
        public MikaTask<UIElement<Transform>> GetUIElement<T>(string name) where T : MonoBehaviour, IVisualUI
        {
            uiElementList ??= GetUIElements(poolRoot);
            UIElement<Transform> result = uiElementList.FirstOrDefault(p => p.GetTemplate().GetType() == typeof(T) && p.UIName == name);
            return MikaTask<UIElement<Transform>>.FromResult(result);


            List<UIElement<Transform>> GetUIElements(Transform poolRoot)
            {
                uiElementList ??= CreateUIElementList(sources, poolRoot);

                return uiElementList;
            }
        }


        static List<UIElement<Transform>> CreateUIElementList(IEnumerable<IUIElementSource> sources, Transform poolRoot)
        {
            return sources
                .Select(CreateUIElement)
                .Where(p => p != null)
                .ToList();


            UIElement<Transform> CreateUIElement(IUIElementSource source)
            {
                IVisualUI mikaUITemplate = source.UITemplate as IVisualUI;
                if (source.UITemplate is IVisualUI == false)
                {
                    Debug.LogError($"UIElementProvider: {source.UITemplate.name} is not IVisualUI");
                    return null;
                }

                Dictionary<IVisualUI, Action> uiCleaner = new();
                Queue<IVisualUI> uiPool = new();
                Dictionary<IVisualUI, Guid> uiIDMap = new();

                Action<IVisualUI> recovery = (ui) =>
                {
                    if ((ui as MonoBehaviour) == null)
                    {
                        uiCleaner.Remove(ui);
                        uiIDMap.Remove(ui);
                        return;
                    }

                    if (ui is IUIEffectable reuseable)
                    {
                        if (uiCleaner.TryGetValue(ui, out var cleaner))
                        {
                            cleaner?.Invoke();
                            uiCleaner.Remove(ui);
                        }
                        uiPool.Enqueue(ui);
                        (ui as MonoBehaviour).transform.SetParent(poolRoot, false);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy((ui as MonoBehaviour).gameObject);
                    }
                };

                return new UIElement<Transform>
                {
                    UIName = source.UIName,
                    GetTemplate = () => mikaUITemplate,
                    Create = (trans) =>
                    {
                        bool isReuseable = source.UITemplate is IUIEffectable;

                        // A pooled object can be destroyed externally after normal
                        // recovery, where no destroy listener is attached anymore.
                        while (uiPool.Count > 0 && (uiPool.Peek() as MonoBehaviour) == null)
                        {
                            var destroyedUI = uiPool.Dequeue();
                            uiCleaner.Remove(destroyedUI);
                            uiIDMap.Remove(destroyedUI);
                        }

                        IVisualUI ui = null;
                        if (isReuseable && uiPool.Count > 0)
                        {
                            ui = uiPool.Dequeue();
                            (ui as MonoBehaviour).transform.SetParent(trans, false);
                        }
                        else
                        {
                            ui = UnityEngine.Object.Instantiate(source.UITemplate, trans) as IVisualUI;
                        }

                        if (uiIDMap.TryGetValue(ui, out var id) == false)
                        {
                            id = Guid.NewGuid();
                            uiIDMap.Add(ui, id);
                        }

                        return MikaTask<(IVisualUI, Action, Guid)>.FromResult((ui, OnCreated, id));

                        void OnCreated()
                        {
                            if (ui is IUIEffectable reuseable)
                            {
                                uiCleaner.TryAdd(ui, reuseable.UseEffect());
                            }
                        }
                    },
                    Recovery = recovery,
                    UnexpectedRecovery = (ui) =>
                    {
                        // OnDestroy can reach a managed child before Unity has
                        // destroyed it. Unity's fake-null check is the sole source
                        // of truth: a surviving child follows normal recovery.
                        if ((ui as MonoBehaviour) == null)
                        {
                            uiCleaner.Remove(ui);
                            uiIDMap.Remove(ui);
                            return;
                        }

                        recovery(ui);
                    },
                };
            }
        }


        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new()
        {
            return new()
            {
                UIName = typeof(T).Name,
                Create = () =>
                {
                    var virtualUI = new T();
                    var elementId = Guid.NewGuid();

                    return MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>.FromResult((virtualUI, OnCreated, elementId));
                },
                Recovery = OnRecovery
            };

            static void OnCreated() { }
            static void OnRecovery(IVirtualUI virtualUI) { }

        }
    }
}
