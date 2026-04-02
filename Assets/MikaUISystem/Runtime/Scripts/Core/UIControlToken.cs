

using System;

namespace MikaUISystem
{
    internal enum UITokenStatus
    {
        None,
        BeforeRecoverying,
        BeforeRecoveryed,
        AfterRecoverying,
        Recoveryed,
    }

    public record UIControlToken : IAsyncDisposable, IDisposable
    {
        public Guid ElementID { get; init; }
        public Guid TokenID { get; init; }
        internal Func<MikaTask> BeforeRecovery { get; init; }

        internal Func<MikaTask> AfterRecovery { get; init; }

        public Func<MikaTask> Recovery { get; init; }

        public int? SortingOrder { get; init; }

        internal UITokenStatus RecoveryStatus { get; set; }

        public async void Dispose()
        {
            await Recovery.Invoke();
        }

        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            if (Recovery != null)
                await Recovery.Invoke();
        }

    }

    public sealed record UIControlToken<T> : UIControlToken where T : IUI
    {
        public T UI { get; init; }
        public void Deconstruct(out T UI, out Func<MikaTask> Recovery)
        {
            UI = this.UI;
            Recovery = this.Recovery;
        }
    }



    public record VirtualUIControlToken : IAsyncDisposable, IDisposable
    {
        public Guid ElementID { get; init; }
        public Guid TokenID { get; init; }
        internal Func<MikaTask> BeforeRecovery { get; init; }

        internal Func<MikaTask> AfterRecovery { get; init; }

        public Func<MikaTask> Recovery { get; init; }

        public void Dispose()
        {
            Recovery?.Invoke();
        }

        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            if (Recovery != null)
                await Recovery.Invoke();
        }


    }

    public sealed record VirtualUIControlToken<T> : VirtualUIControlToken, IDisposable where T : IVirtualUI
    {
        public T UI { get; init; }
        public void Deconstruct(out T UI, out Func<MikaTask> Recovery)
        {
            UI = this.UI;
            Recovery = this.Recovery;
        }
    }

}