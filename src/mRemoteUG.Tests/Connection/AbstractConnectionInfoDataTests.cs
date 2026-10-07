using System;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using NUnit.Framework;


namespace mRemoteUG.Tests.Connection
{
	public class AbstractConnectionInfoDataTests
    {
        private class TestAbstractConnectionInfoData : AbstractConnectionRecord {
	        public TestAbstractConnectionInfoData() : base(Guid.NewGuid().ToString())
	        {
	        }
        }
        private TestAbstractConnectionInfoData _testAbstractConnectionInfoData;

        [SetUp]
        public void Setup()
        {
            _testAbstractConnectionInfoData = new TestAbstractConnectionInfoData();
        }

        [TearDown]
        public void Teardown()
        {
            _testAbstractConnectionInfoData = null;
        }


        [Test]
        public void NameNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Name = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void DescriptionNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Description = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void IconNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Icon = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void PanelNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Panel = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void HostnameNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Hostname = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void UsernameNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Username = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void PasswordNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Password = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void DomainNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Domain = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void ProtocolNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Protocol = ProtocolType.SSH2;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void PuttySessionNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.PuttySession = "SomePuttySession";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void PortNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Port = 9999;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void UseConsoleSessionNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.UseConsoleSession = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdpAuthenticationLevelNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDPAuthenticationLevel = RdpProtocol.AuthenticationLevel.AuthRequired;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void LoadBalanceInfoNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.LoadBalanceInfo = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void UseCredSspNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.UseCredSsp = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayUsageMethodNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayUsageMethod = RdpProtocol.RDGatewayUsageMethod.Always;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayHostnameNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayHostname = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayUseConnectionCredentialsNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayUseConnectionCredentials = RdpProtocol.RDGatewayUseConnectionCredentials.SmartCard;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayUsernameNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayUsername = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayPasswordNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayPassword = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RdGatewayDomainNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RDGatewayDomain = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void ResolutionNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Resolution = RdpProtocol.RDPResolutions.Res1366x768;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void AutomaticResizeNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.AutomaticResize = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void ColorsNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.Colors = RdpProtocol.RDPColors.Colors16Bit;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void CacheBitmapsNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.CacheBitmaps = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void DisplayWallpaperNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.DisplayWallpaper = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void DisplayThemesNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.DisplayThemes = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void EnableFontSmoothingNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.EnableFontSmoothing = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void EnableDesktopCompositionNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.EnableDesktopComposition = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectKeysNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectKeys = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectDiskDrivesNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectDiskDrives = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectPrintersNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectPrinters = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectPortsNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectPorts = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectSmartCardsNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectSmartCards = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectSoundNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectSound = RdpProtocol.RDPSounds.DoNotPlay;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void RedirectClipboardNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.RedirectClipboard = true;
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void MacAddressNotifiesOnValueChange()
        {
            var wasCalled = false;
            _testAbstractConnectionInfoData.PropertyChanged += (sender, args) => wasCalled = true;
            _testAbstractConnectionInfoData.MacAddress = "a";
            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void UserFieldNotifiesOnValueChange()
        {
            var wasCalled = false;
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
    }
}