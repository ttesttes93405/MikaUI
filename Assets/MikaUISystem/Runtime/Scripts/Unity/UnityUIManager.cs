using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MikaUISystem.Plugin;

namespace MikaUISystem
{


    public sealed class UIManager : UIManager<MonoBehaviour, Transform>
    {

        public CanvasProvider CanvasProvider { get; private set; }



        public UIManager(
            IUIElementProvider<MonoBehaviour, Transform> uiElementProvider,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform>> plugins,
            Logger logger = null
            ) : base(
                uiElementProvider,
                plugins,
                logger)
        {
            CanvasProvider = new CanvasProvider(canvasRoot, canvasTemplate);
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
                plugins ?? PluginCreator.Create(),
                logger)
        {
            CanvasProvider = new CanvasProvider(canvasRoot, canvasTemplate);
        }

        public async Task<UIControlToken<T, Transform>> Create<T>() where T : MonoBehaviour, IUI
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


        public async Task<UIControlToken<T, Transform>> Create<T>(int sortingOrder, string name = "") where T : MonoBehaviour, IUI
        {
            var canvas = CanvasProvider.Requset(sortingOrder);
            var slot = new Slot(null, canvas.transform);

            try
            {
                var token = await Create<T>(slot, slotRectConfigs: null, name: name);

                CanvasProvider.Registry(token.TokenID, sortingOrder);

                token.OnDispose += () =>
                {
                    CanvasProvider.Unregistry(token.TokenID);
                };

                return token;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw e;
            }
        }

        public override ValueTask DisposeAsync()
        {
            return base.DisposeAsync();
        }

    }


}