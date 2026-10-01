using System;
using System.Runtime.CompilerServices;

namespace MikaUI
{
    public sealed class DefaultUIInstanceUseLifecycle<TUI> : IUIInstanceUseLifecycle<TUI>
        where TUI : class, IVisualUI
    {
        sealed class UseState
        {
            public readonly Guid ElementId = Guid.NewGuid();
            public Action Cleaner;
            public bool InUse;
            public bool Activated;
            public bool Activating;
            public bool CleanupAfterSetup;
        }

        // Idle objects discarded by a pool must not be kept alive just to remember an ID.
        readonly ConditionalWeakTable<TUI, UseState> states = new();

        public Guid BeginUse(TUI ui)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));

            var state = states.GetValue(ui, _ => new UseState());
            if (state.InUse)
                throw new InvalidOperationException("The UI instance is already in use.");

            state.InUse = true;
            state.Activated = false;
            state.Cleaner = null;
            return state.ElementId;
        }

        public void OnCreated(TUI ui)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));
            if (!states.TryGetValue(ui, out var state) || !state.InUse || state.Activated || state.Activating)
                throw new InvalidOperationException("The UI instance has not begun a new use.");

            Action cleaner;
            state.Activating = true;
            try
            {
                cleaner = (ui as IUIEffectable)?.UseEffect();
            }
            finally
            {
                state.Activating = false;
            }

            // Setup can re-enter Recover before returning its cleanup. The old
            // state is then detached, so never publish the cleanup into a new use.
            if (!state.InUse)
            {
                if (state.CleanupAfterSetup)
                    cleaner?.Invoke();
                return;
            }

            state.Cleaner = cleaner;
            state.Activated = true;
        }

        public bool Recover(TUI ui, bool isAlive)
        {
            if (ui == null)
                throw new ArgumentNullException(nameof(ui));
            if (!states.TryGetValue(ui, out var state) || !state.InUse)
                return false;

            state.InUse = false;
            state.CleanupAfterSetup = state.Activating && isAlive;
            var activated = state.Activated;
            var cleaner = state.Cleaner;
            state.Activated = false;
            state.Cleaner = null;

            if (!isAlive)
            {
                states.Remove(ui);
                return false;
            }

            try
            {
                cleaner?.Invoke();
            }
            catch
            {
                states.Remove(ui);
                throw;
            }

            if (activated && ui is IUIEffectable)
                return true;

            states.Remove(ui);
            return false;
        }
    }
}
