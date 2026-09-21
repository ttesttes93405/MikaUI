
namespace MikaUI
{
    public interface IVirtualSlot
    {
        IBaseUI ParentUI { get; }
    }

    public interface ISlot<TContainer> : IVirtualSlot
    {
        TContainer Container { get; }
    }
}