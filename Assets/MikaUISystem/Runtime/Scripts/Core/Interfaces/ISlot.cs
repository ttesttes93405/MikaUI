
namespace MikaUISystem
{
    public interface IVirtualSlot
    {
        IUI ParentUI { get; }
    }
    public interface ISlot<TContainer> : IVirtualSlot
    {
        TContainer Container { get; }

    }

}