
namespace MikaUI
{
    public record VirtualSlot : IVirtualSlot
    {
        public IBaseUI ParentUI { get; init; }

        public VirtualSlot(IVisualUI parentUI)
        {
            ParentUI = parentUI;
        }

        public VirtualSlot(IVirtualUI parentUI)
        {
            ParentUI = parentUI;
        }

        public override string ToString()
        {
            return $"{nameof(VirtualSlot)}({nameof(ParentUI)}: {ParentUI})";
        }

    }
}