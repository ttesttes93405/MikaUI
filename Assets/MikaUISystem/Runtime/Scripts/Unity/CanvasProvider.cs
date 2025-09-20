using System;
using System.Collections.Generic;
using UnityEngine;

namespace MikaUISystem
{

    public class CanvasProvider : ICanvasProvider
    {
        readonly Transform root;
        readonly Canvas canvasTemplate;

        internal CanvasProvider(RectTransform root, Canvas canvasTemplate)
        {
            this.root = root;
            this.canvasTemplate = canvasTemplate;
        }

        public float DefaultScaleFactor => canvasTemplate.scaleFactor;


        readonly Dictionary<int, Canvas> pool = new();

        readonly Dictionary<int, HashSet<Guid>> canvasUsingRegistry = new();

        internal Canvas Requset(int sortingOrder)
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


        public void Registry(Guid id, int sortingOrder)
        {
            if (canvasUsingRegistry.TryGetValue(sortingOrder, out var uis) == false)
            {
                uis = new();
                canvasUsingRegistry[sortingOrder] = uis;
            }

            uis.Add(id);
        }

        public void Unregistry(Guid id)
        {
            foreach (var (sortingOrder, uis) in canvasUsingRegistry)
            {
                if (uis.Contains(id))
                {
                    uis.Remove(id);
                    if (uis.Count == 0)
                    {
                        var canvas = pool[sortingOrder];
                        RecoverCanvas(canvas);
                    }
                    break;
                }
            }

            void RecoverCanvas(Canvas canvas)
            {
                canvas.enabled = false;
            }
        }


    }

}