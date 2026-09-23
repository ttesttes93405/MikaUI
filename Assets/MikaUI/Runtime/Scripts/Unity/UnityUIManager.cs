using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using MikaUI.Plugin;

namespace MikaUI
{


    public sealed class UnityUIManager : MikaUI.UIManager<MonoBehaviour, Transform, SlotRectConfigs>
    {
        public CanvasProvider CanvasProvider { get; }



        public UnityUIManager(
            IUIElementProvider<MonoBehaviour, Transform> uiElementProvider,
            RectTransform canvasRoot,
            Canvas canvasTemplate,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins,
            Logger logger = null
            ) : this(
                uiElementProvider,
                new CanvasProvider(canvasRoot, canvasTemplate),
                plugins,
                logger)
        { }

        public UnityUIManager(
            IUIElementProvider<MonoBehaviour, Transform> uiElementProvider,
            CanvasProvider canvasProvider,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins,
            Logger logger = null
            ) : base(
                uiElementProvider,
                GetRoot(canvasProvider),
                ResolvePlugins(canvasProvider, plugins),
                logger)
        {
            CanvasProvider = canvasProvider;

            Application.quitting += Dispose;
        }

        static IPlugin<MonoBehaviour, Transform, SlotRectConfigs>[] ResolvePlugins(
            CanvasProvider canvasProvider,
            IEnumerable<IPlugin<MonoBehaviour, Transform, SlotRectConfigs>> plugins)
        {
            if (canvasProvider == null)
            {
                throw new ArgumentNullException(nameof(canvasProvider));
            }

            return (plugins ?? PluginCreator.Create(canvasProvider)).ToArray();
        }

        static RectTransform GetRoot(CanvasProvider canvasProvider)
        {
            if (canvasProvider == null)
            {
                throw new ArgumentNullException(nameof(canvasProvider));
            }

            return canvasProvider.Root;
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
                plugins,
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

                if (token.IsDisposed)
                {
                    throw new ControlTokenDisposedException(token);
                }

                token.OnDispose += () =>
                {
                    CanvasProvider.Unregister(token.TokenID);
                };

                CanvasProvider.Register(token.TokenID, sortingOrder);

                if (token.IsDisposed)
                {
                    throw new ControlTokenDisposedException(token);
                }

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
