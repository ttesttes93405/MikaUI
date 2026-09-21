using UnityEngine;

namespace MikaUI
{
    public record VisualSlot : ISlot<Transform>
    {
        public IBaseUI ParentUI { get; init; }
        public Transform Container { get; init; }

        public VisualSlot(IVisualUI parentUI, Transform container)
        {
            ParentUI = parentUI;
            Container = container;
        }

        public VisualSlot(IVirtualUI parentUI, Transform container)
        {
            ParentUI = parentUI;
            Container = container;
        }

        public override string ToString()
        {
            return $"{nameof(VisualSlot)}({nameof(ParentUI)}: {ParentUI}, {nameof(Container)}: {(Container == null ? "null" : Container.name)})";
        }
    }

}