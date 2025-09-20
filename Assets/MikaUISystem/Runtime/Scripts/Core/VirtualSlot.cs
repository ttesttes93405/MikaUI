
namespace MikaUISystem
{
    public record VirtualSlot : IVirtualSlot
    {
        public IUI ParentUI { get; init; }

        public VirtualSlot(IUI parentUI)
        {
            ParentUI = parentUI;
        }
    }
}