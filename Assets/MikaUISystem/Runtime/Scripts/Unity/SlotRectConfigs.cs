using UnityEngine;

namespace MikaUISystem
{
    public record SlotRectConfigs
    {
        public Vector2 AnchorMin { get; init; }
        public Vector2 AnchorMax { get; init; }
        public Vector2 AnchorPosition { get; init; }
        public Vector2 Pivot { get; init; }
        public Vector2 SizeDelta { get; init; }

        public SlotRectConfigs() { }

        public static SlotRectConfigs FromStretchFill(RectOffset rectOffset)
        {
            return new()
            {
                AnchorMin = new Vector2(0, 0),
                AnchorMax = new Vector2(1, 1),
                Pivot = new Vector2(0.5f, 0.5f),
                SizeDelta = new Vector2(-rectOffset.left - rectOffset.right, -rectOffset.top - rectOffset.bottom),
                AnchorPosition = new Vector2((rectOffset.left - rectOffset.right) / 2f, (rectOffset.bottom - rectOffset.top) / 2f),
            };
        }
    }
}
