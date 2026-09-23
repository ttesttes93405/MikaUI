
using System;
using System.Threading.Tasks;

namespace MikaUI
{
    public interface IBaseUI { }

    public interface IVirtualUI : IBaseUI { }

    public interface IVisualUI : IBaseUI { }

    /// <summary>
    /// UI effect hook - similar to React useEffect.
    /// Executes setup logic when the UI is used and can return a cleanup function to be executed when the UI is recovered.
    /// </summary>
    public interface IUIEffectable : IVisualUI
    {
        /// <summary>
        /// Execute UI effect setup.
        /// </summary>
        /// <returns>Cleanup function (optional) to be executed when the UI is recovered.</returns>
        Action UseEffect();
    }

    /// <summary>
    /// [Obsolete] Use <see cref="IUIEffectable"/> instead.
    /// </summary>
    [Obsolete("Use IUIEffectable instead", false)]
    public interface IUIReuseable : IUIEffectable
    {
        /// <summary>
        /// [Obsolete] Use <see cref="IUIEffectable.UseEffect"/> instead.
        /// </summary>
        [Obsolete("Use IUIEffectable.UseEffect() instead", false)]
        public Action OnUIUse() => UseEffect();
    }

    public interface IUIInit : IVisualUI
    {
        public void Init();
    }
    
    public interface IUITransitionable : IVisualUI
    {
        public ValueTask WaitUntilVisible();
        public ValueTask WaitUntilHidden();
    }

}