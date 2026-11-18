using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Window;

using NUnit.Framework;

using Buttons = mRemoteUG.UI.Window.ConfigToolbarButtons;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests;

/// <summary>
/// The Config window's six toolbar buttons per selected object and view, asked of
/// <see cref="ConfigWindowRules"/> without a form.
/// </summary>
/// <remarks>
/// A wrong state here lets a user edit inheritance where there is nothing to inherit from, or
/// takes the inheritance view away from a connection in a folder.
/// </remarks>
[TestFixture]
internal sealed class ConfigWindowToolbarTests
{
    private static readonly ConfigToolbarButtons[] s_views =
        { Buttons.Properties, Buttons.Inheritance, Buttons.DefaultProperties, Buttons.DefaultInheritance };

    private static IEnumerable<TestCaseData> SetCheckedCases()
    {
        for (int start = 0; start < 16; start++)
        {
            foreach (ConfigToolbarButtons view in s_views)
            {
                foreach (bool value in new[] { true, false })
                {
                    yield return new TestCaseData((ConfigToolbarButtons)start, view, value)
                        .SetName($"From {(ConfigToolbarButtons)start}, {view} set to {value}");
                }
            }
        }
    }

    [TestCaseSource(nameof(SetCheckedCases))]
    public void CheckingAViewUnchecksTheOthersAndUncheckingItLeavesThemAlone(
        ConfigToolbarButtons start,
        ConfigToolbarButtons view,
        bool value)
    {
        ConfigToolbarButtons result = ConfigWindowRules.SetChecked(start, view, value);

        Assert.Multiple(() =>
        {
            foreach (ConfigToolbarButtons other in s_views)
            {
                bool expected = other == view ? value : !value && start.HasFlag(other);
                Assert.That(result.HasFlag(other), Is.EqualTo(expected), other.ToString());
            }
        });
    }

    private static IEnumerable<TestCaseData> AnyChecks() =>
        new[]
            {
                Buttons.None, Buttons.Properties, Buttons.Inheritance, Buttons.DefaultProperties,
                Buttons.DefaultInheritance,
            }
            .Select(checks => new TestCaseData(checks).SetName("Checked: " + checks));

    [TestCaseSource(nameof(AnyChecks))]
    public void TheConnectionsRootShowsItsPropertiesAndOffersTheDefaults(ConfigToolbarButtons checks)
    {
        var root = new RootNodeInfo(RootNodeType.Connection);

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(root, checks);

        AssertState(
            state,
            Buttons.Properties | Buttons.DefaultProperties | Buttons.DefaultInheritance,
            Buttons.Properties,
            root,
            root);
    }

    [TestCaseSource(nameof(AnyChecks))]
    public void ThePuttySessionsRootOffersOnlyItsProperties(ConfigToolbarButtons checks)
    {
        var root = new RootPuttySessionsNodeInfo();

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(root, checks);

        AssertState(state, Buttons.Properties, Buttons.Properties, root, root);
    }

    [Test]
    public void ARootOfAnUnknownTypeIsShownWithEveryButtonOff()
    {
        var root = new RootNodeInfo((RootNodeType)7);

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(root, Buttons.DefaultInheritance);

        AssertState(state, Buttons.None, Buttons.DefaultInheritance, root, root);
    }

    private static IEnumerable<TestCaseData> FolderCases()
    {
        yield return new TestCaseData(null, false).SetName("Folder without a parent");
        yield return new TestCaseData(new RootNodeInfo(RootNodeType.Connection), false)
            .SetName("Folder in the root");
        yield return new TestCaseData(new ContainerInfo(), true).SetName("Folder in a folder");
    }

    [TestCaseSource(nameof(FolderCases))]
    public void AFolderOffersInheritanceOnlyInsideAnotherFolder(ContainerInfo? parent, bool inherits)
    {
        var folder = new ContainerInfo();
        parent?.AddChild(folder);

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(folder, Buttons.DefaultProperties);

        AssertState(
            state,
            Buttons.Properties | Buttons.Icon | (inherits ? Buttons.Inheritance : Buttons.None),
            Buttons.Properties,
            folder,
            folder);
    }

    private static IEnumerable<TestCaseData> ConnectionCases()
    {
        yield return new TestCaseData(new ConnectionInfo(), null, false).SetName("Connection without a parent");
        yield return new TestCaseData(new ConnectionInfo(), new RootNodeInfo(RootNodeType.Connection), false)
            .SetName("Connection in the root");
        yield return new TestCaseData(new ConnectionInfo(), new ContainerInfo(), true)
            .SetName("Connection in a folder");
        yield return new TestCaseData(new PuttySessionInfo(), new RootPuttySessionsNodeInfo(), false)
            .SetName("PuTTY session in its root");
        yield return new TestCaseData(new PuttySessionInfo(), new ContainerInfo(), false)
            .SetName("PuTTY session in a folder");
    }

    [TestCaseSource(nameof(ConnectionCases))]
    public void AConnectionsPropertiesOfferInheritanceOnlyInsideAFolder(
        ConnectionInfo connection,
        ContainerInfo? parent,
        bool inherits)
    {
        parent?.AddChild(connection);

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(connection, Buttons.Properties);

        AssertState(
            state,
            Buttons.Properties | Buttons.Icon | Buttons.HostStatus | (inherits ? Buttons.Inheritance : Buttons.None),
            Buttons.Properties,
            connection,
            connection);
    }

    [Test]
    public void TheDefaultConnectionOffersTheDefaultsAndItsIcon()
    {
        DefaultConnectionInfo defaults = DefaultConnectionInfo.Instance;

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(defaults, Buttons.DefaultProperties);

        AssertState(
            state,
            Buttons.Properties | Buttons.DefaultProperties | Buttons.DefaultInheritance | Buttons.Icon,
            Buttons.DefaultProperties,
            defaults,
            defaults);
    }

    [TestCase(Buttons.DefaultProperties)]
    [TestCase(Buttons.DefaultInheritance)]
    public void AConnectionSelectedInADefaultViewIsShownInItsPropertiesView(ConfigToolbarButtons view)
    {
        var connection = new ConnectionInfo();

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(connection, view);

        AssertState(
            state,
            Buttons.Properties | Buttons.Inheritance | Buttons.Icon | Buttons.HostStatus,
            Buttons.Properties,
            connection,
            connection);
    }

    [Test]
    public void AConnectionSelectedInTheInheritanceViewShowsItsInheritance()
    {
        var connection = new ConnectionInfo();

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(connection, Buttons.Inheritance);

        AssertState(
            state,
            Buttons.Properties | Buttons.Inheritance | Buttons.Icon | Buttons.HostStatus,
            Buttons.Inheritance,
            connection.Inheritance,
            connection);
    }

    [Test]
    public void AConnectionSelectedWithNoViewCheckedLeavesTheGridAlone()
    {
        var connection = new ConnectionInfo();

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(connection, Buttons.None);

        Assert.Multiple(() =>
        {
            Assert.That(state.Enabled, Is.EqualTo(Buttons.None));
            Assert.That(state.Checked, Is.EqualTo(Buttons.None));
            Assert.That(state.ReplacesGridObject, Is.False);
            Assert.That(state.IconSource, Is.SameAs(connection));
        });
    }

    private static IEnumerable<TestCaseData> InheritanceCases()
    {
        yield return new TestCaseData(new ConnectionInfo(), true).SetName("Inheritance of a connection");
        yield return new TestCaseData(new ContainerInfo(), false).SetName("Inheritance of a folder");
    }

    [TestCaseSource(nameof(InheritanceCases))]
    public void InheritanceShownOffersTheHostStatusOnlyForAConnection(ConnectionInfo owner, bool hostStatus)
    {
        ConfigToolbarState state =
            ConfigWindowRules.ToolbarFor(owner.Inheritance, Buttons.Inheritance | Buttons.DefaultInheritance);

        AssertState(
            state,
            Buttons.Properties | Buttons.Inheritance | Buttons.Icon | (hostStatus ? Buttons.HostStatus : Buttons.None),
            Buttons.Inheritance,
            owner.Inheritance,
            owner);
    }

    [Test]
    public void TheDefaultInheritanceOffersTheDefaultsAndNoIcon()
    {
        DefaultConnectionInheritance defaults = DefaultConnectionInheritance.Instance;

        ConfigToolbarState state =
            ConfigWindowRules.ToolbarFor(defaults, Buttons.DefaultInheritance | Buttons.Properties);

        AssertState(
            state,
            Buttons.Properties | Buttons.DefaultProperties | Buttons.DefaultInheritance,
            Buttons.DefaultInheritance,
            defaults,
            null);
    }

    [TestCase(Buttons.None)]
    [TestCase(Buttons.Properties)]
    [TestCase(Buttons.DefaultProperties)]
    public void InheritanceShownOutsideAnInheritanceViewTurnsEveryButtonOff(ConfigToolbarButtons checks)
    {
        ConnectionInfoInheritance inheritance = new ConnectionInfo().Inheritance;

        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(inheritance, checks);

        AssertState(state, Buttons.None, checks, inheritance, null);
    }

    [Test]
    public void InheritanceWithoutAConnectionBehindItCannotBeShownInTheInheritanceView()
    {
        // Unreachable from the window: the default inheritance only arrives through its own
        // button, which checks the default inheritance view first. The window logs the failure.
        Assert.Throws<InvalidOperationException>(
            () => ConfigWindowRules.ToolbarFor(DefaultConnectionInheritance.Instance, Buttons.Inheritance));
    }

    [TestCase(null)]
    [TestCase("not a connection")]
    public void AnythingElseTurnsEveryButtonOffAndLeavesTheGridAlone(object? selected)
    {
        ConfigToolbarState state = ConfigWindowRules.ToolbarFor(selected, Buttons.Inheritance);

        Assert.Multiple(() =>
        {
            Assert.That(state.Enabled, Is.EqualTo(Buttons.None));
            Assert.That(state.Checked, Is.EqualTo(Buttons.Inheritance));
            Assert.That(state.ReplacesGridObject, Is.False);
            Assert.That(state.IconSource, Is.Null);
        });
    }

    private static void AssertState(
        ConfigToolbarState state,
        ConfigToolbarButtons enabled,
        ConfigToolbarButtons checkedViews,
        object gridObject,
        ConnectionInfo? iconSource)
    {
        Assert.Multiple(() =>
        {
            Assert.That(state.Enabled, Is.EqualTo(enabled), "enabled");
            Assert.That(state.Checked, Is.EqualTo(checkedViews), "checked");
            Assert.That(state.ReplacesGridObject, Is.True, "replaces the grid object");
            Assert.That(state.GridObject, Is.SameAs(gridObject), "grid object");
            Assert.That(state.IconSource, Is.SameAs(iconSource), "icon source");
        });
    }
}
