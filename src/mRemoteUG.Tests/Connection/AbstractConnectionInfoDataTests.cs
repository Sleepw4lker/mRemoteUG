using System.Reflection;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;


namespace mRemoteUG.Tests.Connection;

public class AbstractConnectionInfoDataTests
{
    private sealed class TestAbstractConnectionInfoData : AbstractConnectionRecord
    {
        public TestAbstractConnectionInfoData() : base(Guid.NewGuid().ToString())
        {
        }
    }
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private TestAbstractConnectionInfoData _testAbstractConnectionInfoData = null!;

    [SetUp]
    public void Setup()
    {
        _testAbstractConnectionInfoData = new TestAbstractConnectionInfoData();
    }


    [Test]
    public void NameNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Name = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void DescriptionNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Description = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void IconNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Icon = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void PanelNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Panel = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void HostnameNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Hostname = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void UsernameNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Username = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void PasswordNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Password = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void DomainNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Domain = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ProtocolNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Protocol = ProtocolType.SSH2;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void PuttySessionNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.PuttySession = "SomePuttySession";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void PortNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Port = 9999;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void UseConsoleSessionNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.UseConsoleSession = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdpAuthenticationLevelNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDPAuthenticationLevel = RdpProtocol.AuthenticationLevel.AuthRequired;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void LoadBalanceInfoNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.LoadBalanceInfo = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void UseCredSspNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.UseCredSsp = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayUsageMethodNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayUsageMethod = RdpProtocol.RDGatewayUsageMethod.Always;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayHostnameNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayHostname = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayUseConnectionCredentialsNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayUseConnectionCredentials =
            RdpProtocol.RDGatewayUseConnectionCredentials.SmartCard;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayUsernameNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayUsername = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayPasswordNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayPassword = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RdGatewayDomainNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RDGatewayDomain = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ResolutionNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Resolution = RdpProtocol.RDPResolutions.Res1366x768;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void AutomaticResizeNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.AutomaticResize = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ColorsNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.Colors = RdpProtocol.RDPColors.Colors16Bit;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void CacheBitmapsNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.CacheBitmaps = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void DisplayWallpaperNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.DisplayWallpaper = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void DisplayThemesNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.DisplayThemes = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void EnableFontSmoothingNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.EnableFontSmoothing = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void EnableDesktopCompositionNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.EnableDesktopComposition = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectKeysNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectKeys = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectDiskDrivesNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectDiskDrives = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectPrintersNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectPrinters = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectPortsNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectPorts = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectSmartCardsNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectSmartCards = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectSoundNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectSound = RdpProtocol.RDPSounds.DoNotPlay;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RedirectClipboardNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.RedirectClipboard = true;
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void MacAddressNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.MacAddress = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void UserFieldNotifiesOnValueChange()
    {
        bool wasCalled = false;
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
        _testAbstractConnectionInfoData.UserField = "a";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ReadingBackAPropertyReturnsWhatWasSet()
    {
        // Every other test here only ever sets. That mattered: the base GetPropertyValue
        // used to do GetType().GetProperty(name).GetValue(this), which calls the getter
        // that called it - unbounded recursion, and a stack overflow takes the test host
        // with it rather than failing. It was unreachable in the application because
        // ConnectionInfo overrides the method, and unreachable here because nothing read
        // a property back. This reads one.
        _testAbstractConnectionInfoData.Name = "a name";
        Assert.That(_testAbstractConnectionInfoData.Name, Is.EqualTo("a name"));
    }

    /// <summary>
    /// Each setter names its own property in the event it raises, exactly once.
    /// </summary>
    /// <remarks>
    /// The tests above check only that some event fired. The tree and the property grid act on
    /// the name, so a setter that raised an empty or wrong one went unnoticed. The cases are
    /// every read-write property on the record, so a new one is covered without touching this.
    /// </remarks>
    [TestCaseSource(nameof(ReadWriteProperties))]
    public void ASetterRaisesPropertyChangedWithItsOwnName(PropertyInfo property)
    {
        object newValue = DifferentPropertyValue.For(property, property.GetValue(_testAbstractConnectionInfoData));
        var names = new List<string?>();
        _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => names.Add(args.PropertyName);

        property.SetValue(_testAbstractConnectionInfoData, newValue);

        Assert.That(names, Is.EqualTo(new[] { property.Name }));
    }

    /// <summary>The idle timeout is held between 0 and 240 minutes.</summary>
    [TestCase(-5, ExpectedResult = 0)]
    [TestCase(0, ExpectedResult = 0)]
    [TestCase(240, ExpectedResult = 240)]
    [TestCase(300, ExpectedResult = 240)]
    public int TheIdleTimeoutIsClampedOnAssignment(int minutes)
    {
        _testAbstractConnectionInfoData.RDPMinutesToIdleTimeout = minutes;
        return _testAbstractConnectionInfoData.RDPMinutesToIdleTimeout;
    }

    private static IEnumerable<TestCaseData> ReadWriteProperties()
    {
        return typeof(AbstractConnectionRecord)
               .GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(p => p.CanRead && p.CanWrite && p.GetSetMethod() != null)
               .Select(p => new TestCaseData(p).SetArgDisplayNames(p.Name));
    }
}
