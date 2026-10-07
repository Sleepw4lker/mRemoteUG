using System;

namespace mRemoteUG.UI
{
    /// <summary>
    /// Marshals work onto the UI thread.
    /// <para>
    /// This exists because <see cref="System.Windows.Forms.Control.InvokeRequired"/> is a
    /// question about window handles, and during teardown the handles are being destroyed
    /// underneath the caller: once a control's handle is gone, InvokeRequired answers
    /// <see langword="false"/> from any thread, and the caller happily runs UI work - including
    /// releasing an ActiveX control - on whatever thread it happens to be on. Capturing the UI
    /// thread once, while it is still knowable, turns that unanswerable runtime question into a
    /// stored fact.
    /// </para>
    /// </summary>
    public interface IUiThreadInvoker
    {
        /// <summary>
        /// True when the calling thread is the UI thread this invoker was created on.
        /// </summary>
        bool OnUiThread { get; }

        /// <summary>
        /// Queues <paramref name="action"/> on the UI thread and returns immediately. Use this to
        /// get off a COM event callback before touching the object that raised it.
        /// </summary>
        void Post(Action action);

        /// <summary>
        /// Runs <paramref name="action"/> on the UI thread and waits for it to finish.
        /// </summary>
        void Send(Action action);
    }
}
