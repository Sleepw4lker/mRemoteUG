using System.Collections.Specialized;

using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// The guards in the base provider that the registry provider never reaches: sessions without a
/// name, two stored names decoding to one, and removing a session the tree does not have.
/// </summary>
[TestFixture]
public class AbstractPuttySessionsProviderTests
{
    private static readonly string[] s_aBArguments = { "a b" };

    // Rebuilt for every test in Setup: each test starts from an empty provider.
    private FakeProvider _provider = null!;
    private List<NotifyCollectionChangedEventArgs> _changes = null!;

    [SetUp]
    public void Setup()
    {
        _provider = new FakeProvider();
        _changes = new List<NotifyCollectionChangedEventArgs>();
        _provider.PuttySessionsCollectionChanged += (sender, args) => _changes.Add(args);
    }

    [TestCase(null)]
    [TestCase("")]
    public void ASessionWithoutANameIsNotAdded(string name)
    {
        _provider.Stored["stored"] = new PuttySessionInfo { Name = name };

        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetSessions(), Is.Empty);
            Assert.That(_changes, Is.Empty);
        });
    }

    [Test]
    public void AnEncodedNameIsAddedOnlyOnce()
    {
        // The stored name is compared with the decoded names already in the tree, so every
        // read offers "a%20b" again; the duplicate check is what keeps it to one session.
        _provider.Stored["a%20b"] = new PuttySessionInfo { Name = "a b" };

        _provider.GetSessions();
        _provider.GetSessions();

        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetSessions().Select(s => s.Name), Is.EqualTo(s_aBArguments));
            Assert.That(_changes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void RemovingASessionTheTreeDoesNotHaveRaisesNothing()
    {
        _provider.Remove(new PuttySessionInfo { Name = "stranger" });

        Assert.That(_changes, Is.Empty);
    }

    [Test]
    public void StartingAndStoppingTheWatcherDoesNothingByDefault()
    {
        Assert.DoesNotThrow(() =>
        {
            _provider.StartWatcher();
            _provider.StopWatcher();
        });
    }

    [Test]
    public void RaiseSessionChangedEventReachesTheSubscribers()
    {
        var args = new PuttySessionChangedEventArgs();
        object? seenSender = null;
        PuttySessionChangedEventArgs? seenArgs = null;
        _provider.PuttySessionChanged += (sender, e) =>
        {
            seenSender = sender;
            seenArgs = e;
        };

        _provider.Raise(args);

        Assert.Multiple(() =>
        {
            Assert.That(seenSender, Is.SameAs(_provider));
            Assert.That(seenArgs, Is.SameAs(args));
        });
    }

    [Test]
    public void RaisingWithoutSubscribersIsHarmless()
    {
        Assert.DoesNotThrow(() => _provider.Raise(new PuttySessionChangedEventArgs()));
    }

    [Test]
    public void SessionChangedEventArgsCarryTheSession()
    {
        var session = new PuttySessionInfo();

        Assert.Multiple(() =>
        {
            Assert.That(new PuttySessionChangedEventArgs(session).Session, Is.SameAs(session));
            Assert.That(new PuttySessionChangedEventArgs().Session, Is.Null);
        });
    }

    /// <summary>Serves whatever sessions the test puts in <see cref="Stored"/>, by stored name.</summary>
    private sealed class FakeProvider : AbstractPuttySessionsProvider
    {
        public Dictionary<string, PuttySessionInfo> Stored { get; } = new();

        public override string[] GetSessionNames(bool raw = false)
        {
            return Stored.Keys.ToArray();
        }

        public override PuttySessionInfo GetSession(string sessionName)
        {
            return Stored[sessionName];
        }

        public void Remove(PuttySessionInfo session)
        {
            RemoveSession(session);
        }

        public void Raise(PuttySessionChangedEventArgs args)
        {
            RaiseSessionChangedEvent(args);
        }
    }
}
