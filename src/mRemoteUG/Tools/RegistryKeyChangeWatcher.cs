using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace mRemoteUG.Tools
{
    /// <summary>
    /// Raises <see cref="Changed"/> when a registry key, and optionally its subtree, is modified.
    /// </summary>
    /// <remarks>
    /// This replaces a WMI <c>ManagementEventWatcher</c> over <c>RegistryTreeChangeEvent</c>. WMI
    /// was the only reason the application referenced System.Management, which is not part of the
    /// .NET shared framework and therefore had to be shipped alongside the application. It was
    /// also the less reliable option: the WMI query needs the caller's SID and the HKEY_USERS
    /// hive, it depends on the WMI service and a healthy repository, and it polls internally.
    ///
    /// RegNotifyChangeKeyValue is the Win32 API the WMI provider itself is built on. It needs no
    /// service, no elevation and no SID, and it signals an event handle directly.
    ///
    /// Two details of that API drive the shape of this class:
    ///
    /// - Notifications are one-shot. Each time the event signals, the registration has to be
    ///   renewed, which is what <see cref="Arm"/> does on every callback.
    ///
    /// - Without REG_NOTIFY_THREAD_AGNOSTIC the registration is bound to the lifetime of the
    ///   thread that created it, so it would be cancelled as soon as the thread pool retired that
    ///   thread. The flag needs Windows 8 or newer, which the supported baseline satisfies
    ///   several times over.
    ///
    /// The callback arrives on a thread pool thread, matching the WMI watcher this replaces.
    /// </remarks>
    public sealed class RegistryKeyChangeWatcher : IDisposable
    {
        [Flags]
        private enum RegNotifyFilter : uint
        {
            /// <summary>A subkey is added or deleted.</summary>
            Name = 0x00000001,

            /// <summary>A value is added, deleted or changed.</summary>
            LastSet = 0x00000004,

            /// <summary>Deliver notifications independently of the registering thread's lifetime.</summary>
            ThreadAgnostic = 0x10000000
        }

        private const RegNotifyFilter DefaultFilter =
            RegNotifyFilter.Name | RegNotifyFilter.LastSet | RegNotifyFilter.ThreadAgnostic;

        private readonly RegistryKey _rootKey;
        private readonly string _subKeyPath;
        private readonly bool _watchSubtree;
        private readonly object _gate = new object();

        private RegistryKey _watchedKey;
        private ManualResetEvent _changeSignal;
        private RegisteredWaitHandle _waitRegistration;
        private bool _disposed;

        /// <summary>A handler run is in progress on some thread pool thread.</summary>
        private bool _raising;

        /// <summary>A notification arrived while <see cref="_raising"/> was set.</summary>
        private bool _raiseAgain;

        /// <summary>
        /// Raised on a thread pool thread after the watched key changes, never on two threads at
        /// once. A change occurring while a handler runs is reported again once it returns, so a
        /// burst collapses into few runs rather than being lost.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>The key actually being watched, or null until <see cref="Start"/> succeeds.</summary>
        public string WatchedKeyName { get; private set; }

        public RegistryKeyChangeWatcher(RegistryKey rootKey, string subKeyPath, bool watchSubtree = true)
        {
            _rootKey = rootKey ?? throw new ArgumentNullException(nameof(rootKey));
            _subKeyPath = subKeyPath ?? throw new ArgumentNullException(nameof(subKeyPath));
            _watchSubtree = watchSubtree;
        }

        /// <summary>
        /// Begins watching. Returns false when the key does not exist, which is an ordinary
        /// situation rather than an error: PuTTY may simply not be installed.
        /// </summary>
        /// <exception cref="Win32Exception">The key exists but cannot be watched.</exception>
        public bool Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(RegistryKeyChangeWatcher));
                if (_watchedKey != null) return true;

                var key = _rootKey.OpenSubKey(_subKeyPath);
                if (key == null) return false;

                _watchedKey = key;
                WatchedKeyName = key.Name;
                _changeSignal = new ManualResetEvent(false);

                Arm();
                return true;
            }
        }

        /// <summary>Registers for the next change. Caller must hold <see cref="_gate"/>.</summary>
        private void Arm()
        {
            if (_disposed || _watchedKey == null) return;

            var result = RegNotifyChangeKeyValue(_watchedKey.Handle, _watchSubtree, DefaultFilter,
                                                 _changeSignal.SafeWaitHandle, true);
            if (result != 0)
                throw new Win32Exception(result,
                    $"RegNotifyChangeKeyValue failed for '{WatchedKeyName}' (error {result}).");

            _waitRegistration = ThreadPool.RegisterWaitForSingleObject(
                _changeSignal, OnKeyChanged, null, Timeout.Infinite, executeOnlyOnce: true);
        }

        private void OnKeyChanged(object? state, bool timedOut)
        {
            lock (_gate)
            {
                if (_disposed) return;

                // executeOnlyOnce means this registration is spent; release it before renewing.
                _waitRegistration?.Unregister(null);
                _waitRegistration = null;

                _changeSignal.Reset();

                try
                {
                    // Re-arm before raising, so a change occurring while handlers run is not lost.
                    Arm();
                }
                catch (Win32Exception)
                {
                    // The key was most likely deleted. Stop watching rather than spin; the
                    // handlers below still see this final change.
                    _watchedKey?.Dispose();
                    _watchedKey = null;
                }

                if (_raising)
                {
                    // Another pool thread is already inside a handler. Because the registration is
                    // renewed above before handlers run, a burst of changes - measured at 8
                    // concurrent raises for 40 values written to one key - otherwise arrives on as
                    // many threads at once. Handlers of a "something under here changed" edge
                    // re-read the whole key and are not re-entrant, so fold this change into the
                    // run already in flight instead of raising alongside it.
                    _raiseAgain = true;
                    return;
                }

                _raising = true;
            }

            RaiseUntilQuiet();
        }

        /// <summary>
        /// Raises <see cref="Changed"/>, repeating while notifications arrived during the last run.
        /// Called without <see cref="_gate"/> held: handlers can be slow, and one of them marshals
        /// to the UI thread.
        /// </summary>
        private void RaiseUntilQuiet()
        {
            while (true)
            {
                try
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    // Leaving _raising set would silently stop every later notification, so clear
                    // the state before letting the exception out to be reported as usual.
                    lock (_gate)
                    {
                        _raising = false;
                        _raiseAgain = false;
                    }

                    throw;
                }

                lock (_gate)
                {
                    if (!_raiseAgain || _disposed)
                    {
                        _raising = false;
                        _raiseAgain = false;
                        return;
                    }

                    _raiseAgain = false;
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;

                _waitRegistration?.Unregister(null);
                _waitRegistration = null;

                // Closing the key cancels any outstanding notification.
                _watchedKey?.Dispose();
                _watchedKey = null;

                _changeSignal?.Dispose();
                _changeSignal = null;
            }
        }

        // Kept private to this class rather than added to App.NativeMethods: it is the only
        // registry P/Invoke in the application and belongs with the one component that uses it.
        [DllImport("advapi32.dll", EntryPoint = "RegNotifyChangeKeyValue", SetLastError = false)]
        private static extern int RegNotifyChangeKeyValue(
            SafeRegistryHandle hKey,
            [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
            RegNotifyFilter notifyFilter,
            SafeWaitHandle hEvent,
            [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
    }
}
