#nullable enable
using System;
using System.ComponentModel;
using System.Threading;

namespace mRemoteUG.Tools
{
    /// <summary>
    /// Runs one action on a captured <see cref="SynchronizationContext"/>, never concurrently with
    /// itself, folding every request that arrives while a run is outstanding into a single further
    /// run.
    /// </summary>
    /// <remarks>
    /// Written for the PuTTY Profile refresh, which is driven by a registry notification arriving
    /// on a thread pool thread but mutates and enumerates a tree the UI thread reads. Marshalling
    /// the whole refresh onto the UI thread removes the race rather than guarding it, which matters
    /// because the alternative - a lock - deadlocks: the refresh raises collection-changed events
    /// whose handler does a synchronous <c>Control.Invoke</c>, so a background thread holding the
    /// lock would wait on a UI thread waiting for the lock. ADR-0005 covers why a wedged UI thread
    /// is unacceptable here.
    ///
    /// Coalescing is not just an optimisation. Saving one PuTTY profile writes dozens of registry
    /// values, each producing its own notification; without it, one save means dozens of registry
    /// reads and dozens of tree rebuilds.
    ///
    /// <see cref="Post"/> is used rather than <c>Send</c> deliberately: the requesting thread must
    /// not block on the UI thread, and during shutdown there may be no message loop left to answer.
    /// </remarks>
    public sealed class CoalescingDispatcher
    {
        private readonly Action _action;
        private readonly SynchronizationContext? _context;
        private readonly object _gate = new object();

        /// <summary>A run is queued on the context or currently executing.</summary>
        private bool _outstanding;

        /// <summary>A request arrived while <see cref="_outstanding"/> was set.</summary>
        private bool _again;

        private bool _stopped;

        /// <summary>
        /// Captures the calling thread's context. A null context - no message loop, as in tests and
        /// anything running before the UI exists - means the action runs inline on the requesting
        /// thread.
        /// </summary>
        public CoalescingDispatcher(Action action)
            : this(action, SynchronizationContext.Current)
        {
        }

        public CoalescingDispatcher(Action action, SynchronizationContext? context)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _context = context;
        }

        /// <summary>Asks for a run. Safe to call from any thread, as often as wanted.</summary>
        public void Request()
        {
            lock (_gate)
            {
                if (_stopped) return;

                if (_outstanding)
                {
                    _again = true;
                    return;
                }

                _outstanding = true;
            }

            if (_context == null)
            {
                Pump();
                return;
            }

            try
            {
                _context.Post(_ => Pump(), null);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidAsynchronousStateException)
            {
                // The thread the context belonged to is gone, which happens during shutdown.
                // There is nothing left to refresh, so stop rather than retry for ever.
                lock (_gate)
                {
                    _outstanding = false;
                    _again = false;
                    _stopped = true;
                }
            }
        }

        /// <summary>
        /// Makes every later <see cref="Request"/> a no-op. Called at shutdown, once the thing the
        /// action updates is going away.
        /// </summary>
        public void Stop()
        {
            lock (_gate) _stopped = true;
        }

        private void Pump()
        {
            while (true)
            {
                try
                {
                    _action();
                }
                catch
                {
                    // Leaving _outstanding set would silently stop every later run, so clear the
                    // state before letting the exception out to be reported as usual.
                    lock (_gate)
                    {
                        _outstanding = false;
                        _again = false;
                    }

                    throw;
                }

                lock (_gate)
                {
                    if (!_again || _stopped)
                    {
                        _outstanding = false;
                        _again = false;
                        return;
                    }

                    _again = false;
                }
            }
        }
    }
}
