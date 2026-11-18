using System.Diagnostics.CodeAnalysis;

using mRemoteUG.UI.Window;

using NUnit.Framework;


namespace mRemoteUG.Tests.UI.Window;

[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _connectionTreeWindow after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionTreeWindowTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionTreeWindow _connectionTreeWindow = null!;

    [SetUp]
    public void Setup()
    {
        _connectionTreeWindow = new ConnectionTreeWindow();
    }

    [TearDown]
    public void Teardown()
    {
        _connectionTreeWindow.Close();
        _connectionTreeWindow.Dispose();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void CanShowWindow()
    {
        _connectionTreeWindow.Show();
        Assert.That(_connectionTreeWindow.Visible);
    }
}
