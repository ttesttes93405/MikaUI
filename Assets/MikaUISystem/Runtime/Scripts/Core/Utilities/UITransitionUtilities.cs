
namespace MikaUISystem.Core
{
    public static class UITransitionUtilities
    {
        public static async MikaTask TransitionTo<TFrom, TTo, TContainer>(
            this UIControlToken<TFrom, TContainer> from,
            UIControlToken<TTo, TContainer> to,
            bool hideAndDispose)
            where TFrom : IUI
            where TTo : IUITransitionable
        {
            if (to == from)
            {
                return;
            }

            if (to != null)
            {
                await to.UI.WaitUntilVisible();
            }

            if (from != null)
            {
                if (from.UI is IUITransitionable fromTransitionable)
                {
                    await fromTransitionable.WaitUntilHidden();
                }
                if (hideAndDispose)
                {
                    from.Dispose();
                }
            }
        }
    }
}