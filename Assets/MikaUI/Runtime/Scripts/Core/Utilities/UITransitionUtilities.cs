
using System.Threading.Tasks;

namespace MikaUI.Core
{
    public enum UITransitionMode
    {
        /// <summary>
        /// Waits for <c>to.WaitUntilVisible</c> to fully complete before starting <c>from.WaitUntilHidden</c>,
        /// so the two transitions run one after another without overlapping.
        /// </summary>
        //
        //          | <- TransitionTo
        // [  from  ]                       [  from.WaitUntilHidden  ]
        //          [  to.WaitUntilVisible  ][  to  ]
        Sequential,

        /// <summary>
        /// Starts <c>to.WaitUntilVisible</c> and <c>from.WaitUntilHidden</c> at the same time,
        /// so the two transitions run concurrently and overlap.
        /// </summary>
        //          | <- TransitionTo
        // [  from  ][  from.WaitUntilHidden  ]
        //          [  to.WaitUntilVisible  ][  to  ]
        Concurrent
    }

    public static class UITransitionUtilities
    {

        public static async MikaTask TransitionTo<TFrom, TTo, TContainer>(
            this UIControlToken<TFrom, TContainer> from,
            UIControlToken<TTo, TContainer> to,
            bool disposeAfterHidden,
            UITransitionMode mode = UITransitionMode.Sequential)
            where TFrom : IVisualUI
            where TTo : IVisualUI, IUITransitionable
        {
            if (to == from)
            {
                return;
            }

            IUITransitionable fromTransitionable = from switch
            {
                null => null,
                { UI: IUITransitionable t } => t,
                _ => null
            };

            if (mode == UITransitionMode.Concurrent)
            {
                ValueTask? toVisible = to?.UI.WaitUntilVisible();
                ValueTask? fromHidden = fromTransitionable?.WaitUntilHidden();

                if (toVisible != null)
                {
                    await toVisible.Value;
                }
                if (fromHidden != null)
                {
                    await fromHidden.Value;
                }
            }
            else
            {
                if (to != null)
                {
                    await to.UI.WaitUntilVisible();
                }
                if (fromTransitionable != null)
                {
                    await fromTransitionable.WaitUntilHidden();
                }
            }

            if (from != null && disposeAfterHidden)
            {
                from.Dispose();
            }
        }
    }
}