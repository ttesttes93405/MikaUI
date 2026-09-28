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
        readonly Dictionary<int, int> pendingRequests = new();


        public float DefaultScaleFactor => canvasTemplate.scaleFactor;
        public RectTransform Root => root;

        public CanvasProvider(RectTransform root, Canvas canvasTemplate)
        {
            this.root = root;
            this.canvasTemplate = canvasTemplate;
        }


        internal CanvasRequest Request(int sortingOrder)
        {
            if (pool.TryGetValue(sortingOrder, out var canvas) == false)
            {
                canvas = CreateCanvas(sortingOrder);
                pool[sortingOrder] = canvas;
            }

            pendingRequests.TryGetValue(sortingOrder, out var count);
            pendingRequests[sortingOrder] = count + 1;

            canvas.sortingOrder = sortingOrder;

            canvas.enabled = true;
            canvas.gameObject.SetActive(true);

            return new CanvasRequest(this, sortingOrder, canvas);


            Canvas CreateCanvas(int sortingOrder)
            {
                var reuse = Reuse();

                var canvas = reuse == null ? UnityEngine.Object.Instantiate(canvasTemplate, root) : reuse;
                canvas.name = $"Canvas#{sortingOrder}";
                canvas.sortingOrder = sortingOrder;
                return canvas;

                Canvas Reuse()
                {
                    foreach (var (sortingOrder, canvas) in pool)
                    {
                        if (IsUnused(sortingOrder))
                        {
                            pool.Remove(sortingOrder);
                            return canvas;
                        }
                    }
                    return null;
                }
            }
        }

        internal sealed class CanvasRequest : IDisposable
        {
            readonly CanvasProvider provider;
            readonly int sortingOrder;
            bool isDisposed;

            internal CanvasRequest(CanvasProvider provider, int sortingOrder, Canvas canvas)
            {
                this.provider = provider;
                this.sortingOrder = sortingOrder;
                Canvas = canvas;
            }

            internal Canvas Canvas { get; }

            public void Dispose()
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                provider.ReleaseRequest(sortingOrder);
            }
        }

        void ReleaseRequest(int sortingOrder)
        {
            var count = pendingRequests[sortingOrder];
            if (count == 1)
                pendingRequests.Remove(sortingOrder);
            else
                pendingRequests[sortingOrder] = count - 1;

            RecoverIfUnused(sortingOrder);
        }

        bool IsUnused(int sortingOrder)
        {
            return pendingRequests.ContainsKey(sortingOrder) == false
                && (canvasUsingRegistry.TryGetValue(sortingOrder, out var uis) == false || uis.Count == 0);
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
        /// Recovers a canvas once no creation request or active UI uses it.
        /// </summary>
        internal void RecoverIfUnused(int sortingOrder)
        {
            if (IsUnused(sortingOrder) == false)
            {
                return;
            }

            if (pool.TryGetValue(sortingOrder, out var canvas))
            {
                RecoverCanvas(canvas);
            }
        }

        /// <summary>
        /// Recovers every pooled canvas without active UI registrations or requests. This is
        /// called by the unexpected-destruction runner after Unity has completed
        /// its OnDestroy phase, when changing Canvas hierarchy state is safe.
        /// </summary>
        internal void RecoverUnusedCanvases()
        {
            foreach (var (sortingOrder, canvas) in pool)
            {
                if (IsUnused(sortingOrder))
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
                    if (recoverCanvas)
                    {
                        RecoverIfUnused(sortingOrder);
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
