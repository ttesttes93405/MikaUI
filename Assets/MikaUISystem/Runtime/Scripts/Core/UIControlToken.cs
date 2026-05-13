
using System;

namespace MikaUISystem
{
    internal enum NodeStatus
    {
        None,
        Created,
        BeforeRecoveryed,
        Recoveryed,
    }



    public record UIControlToken : IDisposable
    {
        public Guid TokenID { get; init; }
        public Guid ElementID { get; init; }
        public string Name { get; init; }

        public Action Recovery { get; init; }
        [Obsolete("Use Recovery instead.")]
        public Func<MikaTask> RecoveryAsync => () =>
        {
            Recovery?.Invoke();
            return MikaTask.CompletedTask;
        };

        public event Action OnDispose;

        bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                throw new ObjectDisposedException($"Token {Name} ({TokenID}) has already been disposed.");
            Recovery?.Invoke();
            OnDispose?.Invoke();
            IsDisposed = true;
        }

    }

    public sealed record UIControlToken<TUI, TContainer> : UIControlToken where TUI : IUI
    {
        public TUI UI { get; init; }

        public void Deconstruct(out TUI UI, out Action Recovery)
        {
            UI = this.UI;
            Recovery = Dispose;
        }
    }



    public record VirtualUIControlToken : IDisposable
    {
        public Guid TokenID { get; init; }
        public Guid ElementID { get; init; }
        public string Name { get; init; }

        internal Action Recovery { get; init; }

        [Obsolete("Use Recovery instead.")]
        public Func<MikaTask> RecoveryAsync => () =>
        {
            Recovery?.Invoke();
            return MikaTask.CompletedTask;
        };

        public event Action OnDispose;

        bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                throw new ObjectDisposedException($"Token {Name} ({TokenID}) has already been disposed.");

            Recovery?.Invoke();
            OnDispose?.Invoke();
            IsDisposed = true;
        }
    }

    public sealed record VirtualUIControlToken<T> : VirtualUIControlToken, IDisposable where T : IVirtualUI
    {
        public T UI { get; init; }

        public void Deconstruct(out T UI, out Action Recovery)
        {
            UI = this.UI;
            Recovery = Dispose;
        }
    }

}
