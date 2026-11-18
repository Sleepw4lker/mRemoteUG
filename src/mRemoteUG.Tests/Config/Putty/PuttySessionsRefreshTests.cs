using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// What <see cref="AbstractPuttySessionsProvider.GetSessions"/> does to the PuTTY Profile tree
/// when the saved sessions change between two reads, and the events it raises for it.
/// </summary>
/// <remarks>Reads a scratch key (<see cref="PuttyRegistryScratch"/>), never the real PuTTY key.</remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _scratch after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttySessionsRefreshTests
{
    private static readonly string[] s_alphaMySessionZetaArguments = { "alpha", "My Session", "zeta" };
    private static readonly string[] s_alphaBetaArguments = { "alpha", "beta" };
    private static readonly string[] s_newNameArguments = { "new name" };
    private static readonly string[] s_alphaArguments = { "alpha" };
    private static readonly string[] s_defaultSettingsArguments = { "Default Settings" };

    // Rebuilt for every test in Setup: each test gets a scratch key of its own.
    private PuttyRegistryScratch _scratch = null!;
    private PuttySessionsRegistryProvider _provider = null!;
    private List<(object? Sender, NotifyCollectionChangedEventArgs Args)> _changes = null!;

    [SetUp]
    public void Setup()
    {
        _scratch = new PuttyRegistryScratch();
        _provider = _scratch.CreateProvider();
        _changes = new List<(object?, NotifyCollectionChangedEventArgs)>();
        _provider.PuttySessionsCollectionChanged += (sender, args) => _changes.Add((sender, args));
    }

    [TearDown]
    public void TearDown()
    {
        _scratch.Dispose();
    }

    [Test]
    public void FirstReadAddsEverySessionSortedByName()
    {
        AddSshSession("zeta");
        AddSshSession("alpha");
        AddSshSession("My%20Session");

        var sessions = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_alphaMySessionZetaArguments));
            Assert.That(_provider.RootInfo.Children, Is.EqualTo(sessions));
            Assert.That(sessions.Select(s => s.Parent), Is.All.SameAs(_provider.RootInfo));
        });
    }

    [Test]
    public void FirstReadRaisesOneAddForEachSession()
    {
        AddSshSession("alpha");
        AddSshSession("beta");

        var sessions = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(_changes, Has.Count.EqualTo(2));
            Assert.That(_changes.Select(c => c.Sender), Is.All.SameAs(_provider));
            Assert.That(_changes.Select(c => c.Args.Action), Is.All.EqualTo(NotifyCollectionChangedAction.Add));
            Assert.That(_changes.Select(c => c.Args.NewItems?.Cast<PuttySessionInfo>().Single()),
                        Is.EquivalentTo(sessions));
        });
    }

    [Test]
    public void AReadWithoutChangesKeepsTheSessionsAndRaisesNothing()
    {
        AddSshSession("alpha");
        AddSshSession("My%20Session");
        AddSshSession("100%25");
        var first = _provider.GetSessions().ToList();
        _changes.Clear();

        var second = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first), "The same instances should stay in the tree.");
            Assert.That(_changes, Is.Empty);
        });
    }

    [Test]
    public void ASessionSavedBetweenReadsIsAdded()
    {
        AddSshSession("beta");
        _provider.GetSessions();
        _changes.Clear();

        // Sorts before the session already in the tree, so the tree has to be re-sorted.
        AddSshSession("alpha");
        var sessions = _provider.GetSessions().ToList();

        PuttySessionInfo added = sessions.Single(s => s.Name == "alpha");
        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_alphaBetaArguments));
            Assert.That(_changes, Has.Count.EqualTo(1));
            Assert.That(_changes[0].Args.Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
            Assert.That(_changes[0].Args.NewItems?[0], Is.SameAs(added));
        });
    }

    [Test]
    public void ASessionDeletedBetweenReadsIsRemoved()
    {
        AddSshSession("alpha");
        AddSshSession("My%20Session");
        PuttySessionInfo doomed = _provider.GetSessions().Single(s => s.Name == "My Session");
        _changes.Clear();

        _scratch.DeleteSession("My%20Session");
        var sessions = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_alphaArguments));
            Assert.That(_provider.RootInfo.Children, Does.Not.Contain(doomed));
            Assert.That(_changes, Has.Count.EqualTo(1));
            Assert.That(_changes[0].Sender, Is.SameAs(_provider));
            Assert.That(_changes[0].Args.Action, Is.EqualTo(NotifyCollectionChangedAction.Remove));
            Assert.That(_changes[0].Args.OldItems?[0], Is.SameAs(doomed));
        });
    }

    [Test]
    public void ASessionRenamedBetweenReadsIsRemovedAndAddedUnderItsNewName()
    {
        AddSshSession("old%20name");
        PuttySessionInfo old = _provider.GetSessions().Single();
        _changes.Clear();

        // PuTTY has no rename: the user saves under the new name and deletes the old one.
        AddSshSession("new%20name");
        _scratch.DeleteSession("old%20name");
        var sessions = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_newNameArguments));
            Assert.That(_changes.Select(c => c.Args.Action),
                        Is.EquivalentTo(new[]
                        {
                            NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove
                        }));
            Assert.That(_changes.Single(c => c.Args.Action == NotifyCollectionChangedAction.Remove).Args.OldItems?[0],
                        Is.SameAs(old));
            Assert.That(_changes.Single(c => c.Args.Action == NotifyCollectionChangedAction.Add).Args.NewItems?[0],
                        Is.SameAs(sessions[0]));
        });
    }

    [Test]
    public void AnEditedSessionKeepsTheValuesOfItsFirstRead()
    {
        // GetSessions adds and removes; it does not re-read a session it already has. Recorded
        // here so a change to that is a deliberate one.
        AddSshSession("alpha");
        PuttySessionInfo first = _provider.GetSessions().Single();
        _scratch.AddSession("alpha", ("HostName", "changed.example"));

        PuttySessionInfo second = _provider.GetSessions().Single();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Hostname, Is.EqualTo("alpha.example"));
        });
    }

    [Test]
    public void SessionsWithAProtocolThisApplicationCannotOpenAreLeftOut()
    {
        AddSshSession("alpha");
        _scratch.AddSession("console", ("HostName", "COM1"), ("Protocol", "serial"));

        var sessions = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_alphaArguments));
            Assert.That(_changes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void SavedDefaultSettingsBecomeASession()
    {
        _scratch.AddSession("Default%20Settings", ("Protocol", "ssh"), ("SshProt", 3));

        var sessions = _provider.GetSessions().ToList();

        Assert.That(sessions.Select(s => s.Name), Is.EqualTo(s_defaultSettingsArguments));
    }

    [Test]
    public void NoSessionsKeyMeansNoSessions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetSessions(), Is.Empty);
            Assert.That(_changes, Is.Empty);
        });
    }

    [Test]
    public void DeletingTheWholeSessionsKeyRemovesEverySession()
    {
        AddSshSession("alpha");
        AddSshSession("beta");
        _provider.GetSessions();
        _changes.Clear();

        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_scratch.SessionsKeyPath);

        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetSessions(), Is.Empty);
            Assert.That(_changes.Select(c => c.Args.Action),
                        Is.EqualTo(new[]
                        {
                            NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove
                        }));
        });
    }

    private void AddSshSession(string storedName)
    {
        string host = Uri.UnescapeDataString(storedName).Replace(' ', '-') + ".example";
        _scratch.AddSession(storedName, ("HostName", host), ("Protocol", "ssh"), ("SshProt", 3));
    }
}
