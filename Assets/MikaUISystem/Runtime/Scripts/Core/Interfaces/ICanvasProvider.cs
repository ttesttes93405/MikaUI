using System;

namespace MikaUISystem
{
    public interface ICanvasProvider
    {
        public float DefaultScaleFactor { get; }

        public void Register(Guid id, int sortingOrder);

        public void Unregister(Guid id);
    }
}