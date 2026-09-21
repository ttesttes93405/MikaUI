using System;
using System.Collections.Generic;
using UnityEngine;

namespace MikaUI
{

    public class CanvasProvider : ICanvasProvider
    {

        readonly RectTransform root;
        readonly Canvas canvasTemplate;
        readonly Dictionary<int, Canvas> pool = new();
        readonly Dictionary<int, HashSet<Guid>> canvasUsingRegistry = new();


        public float DefaultScaleFactor => canvasTemplate.scaleFactor;
        public RectTransform Root => root;

        public CanvasProvider(RectTransform root, Canvas canvasTemplate)
        {
            this.root = root;
            this.canvasTemplate = canvasTemplate;
        }


        internal Canvas Request(int sortingOrder)
        {
            if (pool.TryGetValue(sortingOrder, out var canvas) == false)
            {
                canvas = CreateCanvas(sortingOrder);
                pool[sortingOrder] = canvas;
            }

            canvas.sortingOrder = sortingOrder;

            canvas.enabled = true;
            canvas.gameObject.SetActive(true);

            return canvas;


            Canvas CreateCanvas(int sortingOrder)
            {
                var reuse = Reuse();

                var canvas = reuse == null ? UnityEngine.Object.Instantiate(canvasTemplate, root) : reuse;
                canvas.name = $"Canvas#{sortingOrder}";
                canvas.sortingOrder = sortingOrder;
                return canvas;

                Canvas Reuse()
                {
                    foreach (var (sortingOrder, lifeTokenSet) in canvasUsingRegistry)
                    {
                        if (lifeTokenSet.Count == 0)
                        {
                            if (pool.TryGetValue(sortingOrder, out var canvas))
                            {
                                pool.Remove(sortingOrder);
                                return canvas;
                            }
                        }
                    }
                    return null;
                }
            }
        }

        public void Register(Guid id, int sortingOrder)
        {
            if (canvasUsingRegistry.TryGetValue(sortingOrder, out var uis) == false)
            {
                uis = new();
                canvasUsingRegistry[sortingOrder] = uis;
            }

            uis.Add(id);

            if (pool.TryGetValue(sortingOrder, out var canvas))
            {
                canvas.enabled = true;
                canvas.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Releases a canvas requested for a UI creation that did not complete.
        /// A concurrent successful creation may register later; Register restores its active state.
        /// </summary>
        internal void RecoverIfUnused(int sortingOrder)
        {
            if (canvasUsingRegistry.TryGetValue(sortingOrder, out var uis) && uis.Count > 0)
            {
                return;
            }

            if (pool.TryGetValue(sortingOrder, out var canvas))
            {
                RecoverCanvas(canvas);
            }
        }

        /// <summary>
        /// Recovers every pooled canvas without active UI registrations. This is
        /// called by the unexpected-destruction runner after Unity has completed
        /// its OnDestroy phase, when changing Canvas hierarchy state is safe.
        /// </summary>
        internal void RecoverUnusedCanvases()
        {
            foreach (var (sortingOrder, uis) in canvasUsingRegistry)
            {
                if (uis.Count == 0 && pool.TryGetValue(sortingOrder, out var canvas))
                {
                    RecoverCanvas(canvas);
                }
            }
        }

        public void Unregister(Guid id)
        {
            Unregister(id, recoverCanvas: true);
        }

        /// <summary>
        /// Removes an unexpectedly destroyed UI from the registry without changing
        /// Canvas state. This is used from a GameObject's OnDestroy callback, where
        /// disabling an ancestor Canvas would mutate a hierarchy being destroyed.
        /// </summary>
        internal void UnregisterWithoutRecovery(Guid id)
        {
            Unregister(id, recoverCanvas: false);
        }

        void Unregister(Guid id, bool recoverCanvas)
        {
            foreach (var (sortingOrder, uis) in canvasUsingRegistry)
            {
                if (uis.Contains(id))
                {
                    uis.Remove(id);
                    if (recoverCanvas && uis.Count == 0)
                    {
                        var canvas = pool[sortingOrder];
                        RecoverCanvas(canvas);
                    }
                    return;
                }
            }

        }

        static void RecoverCanvas(Canvas canvas)
        {
            canvas.enabled = false;
            canvas.gameObject.SetActive(false);
        }

    }

}
