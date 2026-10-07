using System;
using System.Threading;
using System.Windows.Forms;

namespace mRemoteUG.UI
{
    /// <summary>
    /// <see cref="IUiThreadInvoker"/> backed by a WinForms control.
    /// </summary>
    /// <remarks>
    /// Construct this on the UI thread: it captures the current thread as the UI thread.
    /// <paramref name="primary"/> is the natural owner of the work, but its handle may already be
    /// gone by the time we need to marshal (closing a tab destroys it), so a fallback provider
    /// supplies a longer-lived control - normally the main form, whose handle lives as long as the
    /// application.
    /// </remarks>
    public sealed class ControlUiThreadInvoker : IUiThreadInvoker
    {
        private readonly int _uiThreadId;
        private readonly Control _primary;
        private readonly Func<Control> _fallbackProvider;

        public ControlUiThreadInvoker(Control primary, Func<Control> fallbackProvider = null)
        {
            _primary = primary;
            _fallbackProvider = fallbackProvider;
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool OnUiThread => Thread.CurrentThread.ManagedThreadId == _uiThreadId;

        public void Post(Action action)
        {
            if (action == null) return;

            var marshaler = LiveMarshaler();
            if (marshaler == null)
            {
                // Nothing left to marshal through. Running inline is the best we can do, and is
                // correct when we are already on the UI thread.
                action();
                return;
            }

            try
            {
                marshaler.BeginInvoke(action);
            }
            catch (Exception)
            {
                // The handle died between the check and the call.
                action();
            }
        }

        public void Send(Action action)
        {
            if (action == null) return;

            if (OnUiThread)
            {
                action();
                return;
            }

            var marshaler = LiveMarshaler();
            if (marshaler == null)
            {
                action();
                return;
            }

            try
            {
                marshaler.Invoke(action);
            }
            catch (Exception)
            {
                action();
            }
        }

        private Control LiveMarshaler()
        {
            if (IsUsable(_primary))
                return _primary;

            var fallback = _fallbackProvider?.Invoke();
            return IsUsable(fallback) ? fallback : null;
        }

        private static bool IsUsable(Control control)
        {
            return control != null && !control.IsDisposed && !control.Disposing && control.IsHandleCreated;
        }
    }
}
