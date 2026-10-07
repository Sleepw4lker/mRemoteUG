using System;
using System.Threading;
using Microsoft.Win32;
using mRemoteUG.Tools;
using NUnit.Framework;

namespace mRemoteUG.Tests.Tools
{
    /// <summary>
    /// Exercises the registry watcher against a real key under HKCU.
    /// </summary>
    /// <remarks>
    /// The WMI watcher this replaced could not be tested here at all: it needed the WMI service,
    /// a healthy repository and HKEY_USERS, and it failed outright on this development machine.
    /// RegNotifyChangeKeyValue needs none of that, so the behaviour is now verifiable locally.
    /// </remarks>
    [TestFixture]
    public class RegistryKeyChangeWatcherTests
    {
        private const string TestRootPath = @"Software\mRemoteUG.Tests";

        private string _keyPath;
        private RegistryKeyChangeWatcher _watcher;

        /// <summary>Generous: registry notifications are delivered asynchronously by the kernel.</summary>
        private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);

        [SetUp]
        public void Setup()
        {
            _keyPath = $@"{TestRootPath}\{Guid.NewGuid():N}";
            Registry.CurrentUser.CreateSubKey(_keyPath)?.Dispose();
        }

        [TearDown]
        public void TearDown()
        {
            _watcher?.Dispose();
            _watcher = null;
            try { Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false); }
            catch (ArgumentException) { }
        }

        private ManualResetEventSlim WatchAndCaptureSignal(string path = null, bool watchSubtree = true)
        {
            var signalled = new ManualResetEventSlim(false);
            _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, path ?? _keyPath, watchSubtree);
            _watcher.Changed += (s, e) => signalled.Set();
            Assert.That(_watcher.Start(), Is.True, "The watcher did not start.");
            return signalled;
        }

        [Test]
        public void StartReturnsFalseForAKeyThatDoesNotExist()
        {
            _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, $@"{TestRootPath}\{Guid.NewGuid():N}");
            Assert.That(_watcher.Start(), Is.False);
        }

        [Test]
        public void StartReportsTheKeyItIsWatching()
        {
            WatchAndCaptureSignal();
            Assert.That(_watcher.WatchedKeyName, Does.EndWith(_keyPath.Replace('/', '\\')));
        }

        [Test]
        public void RaisesChangedWhenAValueIsSet()
        {
            var signalled = WatchAndCaptureSignal();

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                key.SetValue("HostName", "example.test");

            Assert.That(signalled.Wait(SignalTimeout), Is.True, "No change notification arrived.");
        }

        [Test]
        public void RaisesChangedWhenASubkeyIsCreated()
        {
            // This is the case that matters: PuTTY stores each session as a subkey.
            var signalled = WatchAndCaptureSignal();

            Registry.CurrentUser.CreateSubKey($@"{_keyPath}\a-new-session")?.Dispose();

            Assert.That(signalled.Wait(SignalTimeout), Is.True, "No change notification arrived.");
        }

        [Test]
        public void RaisesChangedWhenASubkeyIsDeleted()
        {
            Registry.CurrentUser.CreateSubKey($@"{_keyPath}\doomed-session")?.Dispose();
            var signalled = WatchAndCaptureSignal();

            Registry.CurrentUser.DeleteSubKeyTree($@"{_keyPath}\doomed-session");

            Assert.That(signalled.Wait(SignalTimeout), Is.True, "No change notification arrived.");
        }

        [Test]
        public void RaisesChangedForAChangeInsideASubkeyWhenWatchingTheSubtree()
        {
            Registry.CurrentUser.CreateSubKey($@"{_keyPath}\session")?.Dispose();
            var signalled = WatchAndCaptureSignal();

            using (var key = Registry.CurrentUser.OpenSubKey($@"{_keyPath}\session", writable: true))
                key.SetValue("PortNumber", 2222);

            Assert.That(signalled.Wait(SignalTimeout), Is.True,
                        "A change below the watched key should be reported when watching the subtree.");
        }

        [Test]
        public void KeepsRaisingChangedAfterTheFirstNotification()
        {
            // Registry notifications are one-shot, so this is the behaviour most likely to be
            // wrong: without re-arming, only the first change would ever be reported.
            var changes = 0;
            var third = new ManualResetEventSlim(false);

            _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
            _watcher.Changed += (s, e) =>
            {
                if (Interlocked.Increment(ref changes) >= 3) third.Set();
            };
            Assert.That(_watcher.Start(), Is.True);

            for (var i = 0; i < 3; i++)
            {
                using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                    key.SetValue($"Value{i}", i);
                Thread.Sleep(50);
            }

            Assert.That(third.Wait(SignalTimeout), Is.True,
                        $"Expected at least 3 notifications, saw {Volatile.Read(ref changes)}.");
        }

        [Test]
        public void NeverRaisesChangedConcurrentlyWithItself()
        {
            // Saving one PuTTY profile writes dozens of values, each of which is a separate
            // one-shot notification. Because the registration is renewed before handlers run,
            // those notifications land on different thread pool threads; a handler that re-reads
            // the whole key is not re-entrant, so the watcher has to serialise them itself.
            var concurrent = 0;
            var maxConcurrent = 0;
            var raises = 0;

            _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
            _watcher.Changed += (s, e) =>
            {
                InterlockedMax(ref maxConcurrent, Interlocked.Increment(ref concurrent));
                Thread.Sleep(20);
                Interlocked.Increment(ref raises);
                Interlocked.Decrement(ref concurrent);
            };
            Assert.That(_watcher.Start(), Is.True);

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
            {
                for (var i = 0; i < 40; i++)
                    key.SetValue($"Value{i}", i);
            }

            WaitUntil(() => Volatile.Read(ref raises) >= 1 && Volatile.Read(ref concurrent) == 0);

            Assert.That(Volatile.Read(ref maxConcurrent), Is.EqualTo(1),
                        "Changed must never be raised on two threads at once.");
        }

        [Test]
        public void StillRaisesChangedForAChangeThatArrivesWhileAHandlerIsRunning()
        {
            // Serialising must coalesce rather than drop: a change made while a handler runs has
            // to be reported afterwards, or the tree silently goes stale.
            var firstRaiseStarted = new ManualResetEventSlim(false);
            var releaseFirstRaise = new ManualResetEventSlim(false);
            var raises = 0;

            _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
            _watcher.Changed += (s, e) =>
            {
                if (Interlocked.Increment(ref raises) == 1)
                {
                    firstRaiseStarted.Set();
                    releaseFirstRaise.Wait(SignalTimeout);
                }
            };
            Assert.That(_watcher.Start(), Is.True);

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                key.SetValue("First", 1);
            Assert.That(firstRaiseStarted.Wait(SignalTimeout), Is.True, "The first notification never arrived.");

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                key.SetValue("Second", 2);
            Thread.Sleep(100);
            releaseFirstRaise.Set();

            WaitUntil(() => Volatile.Read(ref raises) >= 2);
        }

        [Test]
        public void StopsRaisingChangedOnceDisposed()
        {
            var signalled = WatchAndCaptureSignal();
            _watcher.Dispose();
            _watcher = null;
            signalled.Reset();

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                key.SetValue("AfterDispose", "value");

            Assert.That(signalled.Wait(TimeSpan.FromSeconds(1)), Is.False,
                        "A disposed watcher must not keep raising events.");
        }

        [Test]
        public void DisposingTwiceIsHarmless()
        {
            WatchAndCaptureSignal();
            _watcher.Dispose();
            Assert.DoesNotThrow(() => _watcher.Dispose());
            _watcher = null;
        }

        [Test]
        public void StartAfterDisposeThrows()
        {
            WatchAndCaptureSignal();
            var watcher = _watcher;
            _watcher = null;
            watcher.Dispose();

            Assert.Throws<ObjectDisposedException>(() => watcher.Start());
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while ((seen = Volatile.Read(ref target)) < value)
                if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                    return;
        }

        private static void WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + SignalTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                Thread.Sleep(5);
            }

            Assert.Fail("Timed out waiting for the expected state.");
        }
    }
}
