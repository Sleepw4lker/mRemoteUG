using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests;

[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _configWindow after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public abstract class ConfigWindowSpecialTestsBase
{
    protected abstract ProtocolType Protocol { get; }
    protected bool TestAgainstContainerInfo { get; set; }
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConfigWindow _configWindow = null!;
    protected ConnectionInfo ConnectionInfo { get; private set; } = null!;
    protected List<string> ExpectedPropertyList { get; private set; } = null!;

    [SetUp]
    public virtual void Setup()
    {
        ConnectionInfo = ConfigWindowGeneralTests.ConstructConnectionInfo(Protocol, TestAgainstContainerInfo);
        ExpectedPropertyList =
            ConfigWindowGeneralTests.BuildExpectedConnectionInfoPropertyList(Protocol, TestAgainstContainerInfo);

        _configWindow = new ConfigWindow
        {
            PropertiesVisible = true,
        };
    }

    public void RunVerification()
    {
        _configWindow.SelectedTreeNode = ConnectionInfo;
        Assert.That(
            _configWindow.VisibleObjectProperties,
            Is.EquivalentTo(ExpectedPropertyList));
    }

    [TearDown]
    public void TearDown()
    {
        _configWindow.Dispose();
    }
}
