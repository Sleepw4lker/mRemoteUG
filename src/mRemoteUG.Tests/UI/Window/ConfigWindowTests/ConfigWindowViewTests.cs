using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Window;

using NUnit.Framework;

using Buttons = mRemoteUG.UI.Window.ConfigToolbarButtons;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests;

/// <summary>
/// That the Config window puts what <see cref="ConfigWindowRules"/> decides onto its buttons.
/// The rules themselves are <see cref="ConfigWindowToolbarTests"/>.
/// </summary>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _configWindow after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
internal sealed class ConfigWindowViewTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConfigWindow _configWindow = null!;

    [SetUp]
    public void Setup()
    {
        _configWindow = new ConfigWindow();
    }

    [TearDown]
    public void TearDown()
    {
        _configWindow.Dispose();
    }

    private static IEnumerable<TestCaseData> ViewCases()
    {
        yield return new TestCaseData(Buttons.Properties).SetName("Properties");
        yield return new TestCaseData(Buttons.Inheritance).SetName("Inheritance");
        yield return new TestCaseData(Buttons.DefaultProperties).SetName("Default properties");
        yield return new TestCaseData(Buttons.DefaultInheritance).SetName("Default inheritance");
    }

    [TestCaseSource(nameof(ViewCases))]
    public void EachViewPropertyChecksItsOwnButtonOnly(ConfigToolbarButtons view)
    {
        SetVisible(view, true);

        Assert.Multiple(() =>
        {
            Assert.That(_configWindow.CheckedViews, Is.EqualTo(view));
            Assert.That(_configWindow.PropertiesVisible, Is.EqualTo(view == Buttons.Properties));
            Assert.That(_configWindow.InheritanceVisible, Is.EqualTo(view == Buttons.Inheritance));
            Assert.That(_configWindow.DefaultPropertiesVisible, Is.EqualTo(view == Buttons.DefaultProperties));
            Assert.That(_configWindow.DefaultInheritanceVisible, Is.EqualTo(view == Buttons.DefaultInheritance));
        });
    }

    [TestCaseSource(nameof(ViewCases))]
    public void EachViewPropertyUnchecksItsOwnButtonOnly(ConfigToolbarButtons view)
    {
        SetVisible(view, true);

        SetVisible(view, false);

        Assert.That(_configWindow.CheckedViews, Is.EqualTo(Buttons.None));
    }

    private static IEnumerable<TestCaseData> SelectionCases()
    {
        yield return new TestCaseData(new RootNodeInfo(RootNodeType.Connection), null,
                                      Buttons.Properties | Buttons.DefaultProperties | Buttons.DefaultInheritance)
            .SetName("The connections root");
        yield return new TestCaseData(new ContainerInfo(), new ContainerInfo(),
                                      Buttons.Properties | Buttons.Inheritance | Buttons.Icon)
            .SetName("A folder in a folder");
        yield return new TestCaseData(new ConnectionInfo(), new ContainerInfo(),
                                      Buttons.Properties | Buttons.Inheritance | Buttons.Icon | Buttons.HostStatus)
            .SetName("A connection in a folder");
        yield return new TestCaseData(new ConnectionInfo(), new RootNodeInfo(RootNodeType.Connection),
                                      Buttons.Properties | Buttons.Icon | Buttons.HostStatus)
            .SetName("A connection in the root");
    }

    [TestCaseSource(nameof(SelectionCases))]
    public void SelectingANodeSetsTheButtonsTheRulesGive(
        ConnectionInfo node,
        ContainerInfo? parent,
        ConfigToolbarButtons enabled)
    {
        parent?.AddChild(node);
        _configWindow.InheritanceVisible = true;
        _configWindow.PropertiesVisible = true;

        _configWindow.SelectedTreeNode = node;

        Assert.Multiple(() =>
        {
            Assert.That(_configWindow.EnabledButtons, Is.EqualTo(enabled));
            Assert.That(_configWindow.CheckedViews, Is.EqualTo(Buttons.Properties));
        });
    }

    [Test]
    public void SelectingNothingTurnsEveryButtonOff()
    {
        _configWindow.SelectedTreeNode = new ConnectionInfo();

        _configWindow.SelectedTreeNode = null;

        Assert.That(_configWindow.EnabledButtons, Is.EqualTo(Buttons.None));
    }

    [Test]
    public void SelectingAConnectionInTheInheritanceViewKeepsThatView()
    {
        var connection = new ConnectionInfo();
        new ContainerInfo().AddChild(connection);
        _configWindow.InheritanceVisible = true;

        _configWindow.SelectedTreeNode = connection;

        Assert.Multiple(() =>
        {
            Assert.That(
                _configWindow.EnabledButtons,
                Is.EqualTo(Buttons.Properties | Buttons.Inheritance | Buttons.Icon | Buttons.HostStatus));
            Assert.That(_configWindow.CheckedViews, Is.EqualTo(Buttons.Inheritance));
        });
    }

    private void SetVisible(ConfigToolbarButtons view, bool value)
    {
        switch (view)
        {
            case Buttons.Properties:
                _configWindow.PropertiesVisible = value;
                break;
            case Buttons.Inheritance:
                _configWindow.InheritanceVisible = value;
                break;
            case Buttons.DefaultProperties:
                _configWindow.DefaultPropertiesVisible = value;
                break;
            case Buttons.DefaultInheritance:
                _configWindow.DefaultInheritanceVisible = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(view), view, "Not a view button.");
        }
    }
}
