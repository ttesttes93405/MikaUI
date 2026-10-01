
using System;
using System.Collections.Generic;

namespace MikaUI
{

    /// <summary>
    /// A UI lifetime handle with reference identity. All references share its disposal state.
    /// </summary>
    public class UIControlToken : IDisposable
    {

        readonly Guid tokenID;
        readonly Guid elementID;
        readonly string name;
        readonly Action recovery;

        public Guid TokenID => tokenID;
        public Guid ElementID => elementID;
        public string Name => name;
        public Action Recovery => GetValue(recovery);

        /// <summary>
        /// Runs once after disposal. All callbacks are attempted in subscription order;
        /// callback failures are aggregated after notification completes.
        /// </summary>
        public event Action OnDispose;

        public bool IsDisposed { get; private set; }

        internal UIControlToken(Guid tokenID, Guid elementID, string name, Action recovery)
        {
            this.tokenID = tokenID;
            this.elementID = elementID;
            this.name = name;
            this.recovery = recovery;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            recovery?.Invoke();

            CompleteDispose();
        }

        /// <summary>
        /// Completes token disposal after its UI node has recovered. This is also used by
        /// parent-tree recovery so child tokens accurately reflect their UI lifetime.
        /// </summary>
        internal void CompleteDispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            var handlers = OnDispose;
            if (handlers == null)
                return;

            List<Exception> errors = null;
            foreach (Action callback in handlers.GetInvocationList())
            {
                try
                {
                    callback();
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }

            // Preserve error reporting without allowing observers to skip cleanup.
            if (errors != null)
                throw new AggregateException("OnDispose callbacks failed.", errors);
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ControlTokenDisposedException(this);
        }

        protected T GetValue<T>(T value)
        {
            ThrowIfDisposed();
            return value;
        }

    }

    public class UIControlToken<TUI> : UIControlToken where TUI : IBaseUI
    {
        readonly TUI ui;
        public TUI UI => GetValue(ui);

        internal UIControlToken(Guid tokenID, Guid elementID, string name, TUI ui, Action recovery) : base(tokenID, elementID, name, recovery)
        {
            this.ui = ui;
        }

        public void Deconstruct(out TUI UI, out Action Recovery)
        {
            UI = this.UI;
            Recovery = Dispose;
        }
    }

    public sealed class UIControlToken<TUI, TContainer> : UIControlToken<TUI> where TUI : IVisualUI
    {
        internal UIControlToken(Guid tokenID, Guid elementID, string name, TUI ui, Action recovery) : base(tokenID, elementID, name, ui, recovery)
        {
        }
    }

}
