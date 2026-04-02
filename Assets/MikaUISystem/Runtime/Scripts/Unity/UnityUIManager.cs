using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MikaUISystem.Plugin;

namespace MikaUISystem
{


    public sealed class UIManager : UIManager<MonoBehaviour, Transform>
    {

        readonly CanvasProvider canvasProvider;

        public UIManager(
            IUIElementProvider<MonoBehaviour, Transform> uiElementProvider,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform>> plugins,
            Logger logger = null
            ) : base(
                uiElementProvider,
                new CanvasProvider(canvasRoot, canvasTemplate),
                plugins,
                logger)
        {
            canvasProvider = CanvasProvider as CanvasProvider;
        }


        public UIManager(
            IEnumerable<DefaultUIElementProvider.IUIElementSource> uIElementSources,
            Transform poolRoot,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform>> plugins = null,
            Logger logger = null
            ) : base(
                new DefaultUIElementProvider(uIElementSources, poolRoot),
                new CanvasProvider(canvasRoot, canvasTemplate),
                plugins ?? PluginCreator.Create(),
                logger)
        {
            canvasProvider = CanvasProvider as CanvasProvider;
        }


        readonly Dictionary<Type, IUI> uniqueUIs = new();

        public async Task<T> Unique<T>() where T : MonoBehaviour, IUI
        {
            if (uniqueUIs.TryGetValue(typeof(T), out var ui) && ui != null)
            {
                return ui as T;
            }


            (var newUI, var recoveryNewUI) = await Create<T>(0);    // Unique UI will never be recovered.

            uniqueUIs[typeof(T)] = newUI;

            return newUI;

        }

        public async Task<UIControlToken<T>> Create<T>() where T : MonoBehaviour, IUI
        {
            try
            {
                return await Create<T>(0);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw e;
            }
        }

        public async Task<UIControlToken<T>> Create<T>(int sortingOrder, string name = "") where T : MonoBehaviour, IUI
        {
            var canvas = canvasProvider.Requset(sortingOrder);
            try
            {
                return await InternalCreate<T>(name, canvas.transform, null, sortingOrder, null);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw e;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            uniqueUIs.Clear();
            
            await base.DisposeAsync();
        }

    }


}