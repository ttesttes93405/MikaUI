using System;

namespace MikaUISystem
{
    public interface ICanvasProvider
    {
        public float DefaultScaleFactor { get; }

        public void Registry(Guid id, int sortingOrder);

        public void Unregistry(Guid id);

    }
}