using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MikaUISystem
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
        public UIElement<Transform> GetUIElement<T>(string name) where T : MonoBehaviour, IUI
        {
            uiElementList ??= GetUIElements(poolRoot);
            return uiElementList.FirstOrDefault(p => p.GetTemplate().GetType() == typeof(T) && p.UIName == name);


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
                IUI mikaUITemplate = source.UITemplate as IUI;
                if (source.UITemplate is IUI == false)
                {
                    Debug.LogError($"UIElementProvider: {source.UITemplate.name} is not IUI");
                    return null;
                }

                Dictionary<IUI, Action> uiCleaner = new();
                Queue<IUI> uiPool = new();
                Dictionary<IUI, Guid> uiIDMap = new();


                return new UIElement<Transform>
                {
                    UIName = source.UIName,
                    GetTemplate = () => mikaUITemplate,
                    Create = (trans) =>
                    {
                        bool isReuseable = source.UITemplate is IUIReuseable;

                        IUI ui = null;
                        if (isReuseable && uiPool.Count > 0)
                        {
                            ui = uiPool.Dequeue();
                            (ui as MonoBehaviour).transform.SetParent(trans, false);
                        }
                        else
                        {
                            ui = UnityEngine.Object.Instantiate(source.UITemplate, trans) as IUI;
                        }

                        if (uiIDMap.TryGetValue(ui, out var id) == false)
                        {
                            id = Guid.NewGuid();
                            uiIDMap.Add(ui, id);
                        }

                        return MikaTask<(IUI, Action, Guid)>.FromResult((ui, OnCreated, id));

                        void OnCreated()
                        {
                            if (ui is IUIReuseable reuseable)
                            {
                                uiCleaner.TryAdd(ui, reuseable.OnUIUse());
                            }
                        }
                    },
                    Recovery = (ui) =>
                    {
                        if (ui is IUIReuseable reuseable)
                        {
                            if (uiCleaner.TryGetValue(ui, out var cleaner))
                            {
                                cleaner();
                                uiCleaner.Remove(ui);
                            }
                            uiPool.Enqueue(ui);
                            (ui as MonoBehaviour).transform.SetParent(poolRoot, false);
                        }
                        else
                        {
                            UnityEngine.Object.Destroy((ui as MonoBehaviour).gameObject);
                        }

                        return MikaTask.CompletedTask;
                    }
                };
            }
        }


        public VirtualUIElement GetVirtualUIElement<T>() where T : IVirtualUI, new()
        {
            Dictionary<IVirtualUI, Guid> uiIDMap = new();

            return new()
            {
                UIName = typeof(T).Name,
                Create = () =>
                {
                    var virtualUI = new T();
                    var id = uiIDMap.GetValueOrDefault(virtualUI, Guid.NewGuid());

                    return MikaTask<(IVirtualUI ui, Action onCreated, Guid elementID)>.FromResult((virtualUI, OnCreated, id));
                },
                Recovery = OnRecovery
            };

            static void OnCreated() { }
            static void OnRecovery(IVirtualUI virtualUI) { }

        }
    }
}