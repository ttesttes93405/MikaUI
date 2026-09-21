using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MikaUI.Plugin;

namespace MikaUI
{


    public sealed class UnityUIManager : MikaUI.UIManager<MonoBehaviour, Transform, SlotRectConfigs>
    {

        public CanvasProvider CanvasProvider { get; private set; }



        public UnityUIManager(
            IUIElementProvider<MonoBehaviour, Transform> uiElementProvider,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins,
            Logger logger = null
            ) : base(
                uiElementProvider,
                plugins,
                logger)
        {
            CanvasProvider = new CanvasProvider(canvasRoot, canvasTemplate);

            Application.quitting += Dispose;
        }


        public UnityUIManager(
            IEnumerable<DefaultUIElementProvider.IUIElementSource> uIElementSources,
            Transform poolRoot,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins = null,
            Logger logger = null
            ) : this(
                new DefaultUIElementProvider(uIElementSources, poolRoot),
                canvasRoot,
                canvasTemplate,
                plugins ?? PluginCreator.Create(),
                logger)
        { }

        public async Task<UIControlToken<T, Transform>> Create<T>() where T : MonoBehaviour, IVisualUI
        {
            try
            {
                return await Create<T>(0);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }


        public async Task<UIControlToken<T, Transform>> Create<T>(int sortingOrder, string name = "") where T : MonoBehaviour, IVisualUI
        {
            var canvas = CanvasProvider.Request(sortingOrder);
            var slot = new VisualSlot((IVirtualUI)null, canvas.transform);

            try
            {
                var token = await Create<T>(slot, slotRectConfigs: null, name: name);

                CanvasProvider.Register(token.TokenID, sortingOrder);

                token.OnDispose += () =>
                {
                    CanvasProvider.Unregister(token.TokenID);
                };

                return token;
            }
            catch (Exception e)
            {
                CanvasProvider.RecoverIfUnused(sortingOrder);
                Debug.LogError(e);
                throw;
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Application.quitting -= Dispose;
        }

    }


}
