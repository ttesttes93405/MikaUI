using UnityEngine;

namespace MikaUISystem
{
    public record Slot : VirtualSlot, ISlot<Transform>
    {
        public Transform Container { get; init; }

        public Slot(IUI parentUI, Transform container) : base(parentUI)
        {
            Container = container;
        }

        public override string ToString()
        {
            return $"{nameof(Slot)}({nameof(ParentUI)}: {ParentUI}, {nameof(Container)}: {(Container == null ? "null" : Container.name)})";
        }
    }

}