
using System;
using System.Threading.Tasks;

namespace MikaUISystem
{
    public interface IVirtualUI { }

    public interface IUI : IVirtualUI { }

    public interface IUIReuseable : IUI
    {
        public Action OnUIUse();
    }

    public interface IUIInit : IUI
    {
        public void Init();
    }
    
    public interface IUITransitionable : IUI
    {
        public ValueTask WaitUntilVisible();
        public ValueTask WaitUntilHidden();
    }

}