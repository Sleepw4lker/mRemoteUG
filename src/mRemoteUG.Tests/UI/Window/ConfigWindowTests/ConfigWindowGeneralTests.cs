using System;
using System.Collections.Generic;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests
{
    public class ConfigWindowGeneralTests
    {
        private ConfigWindow _configWindow;

        [SetUp]
        public void Setup()
        {
            _configWindow = new ConfigWindow
            {
                PropertiesVisible = true
            };
        }

        [TestCaseSource(nameof(ConnectionInfoGeneralTestCases))]
        public void PropertyGridShowCorrectPropertiesForConnectionInfo(ConnectionInfo connectionInfo, IEnumerable<string> expectedVisibleProperties)
        {
            _configWindow.SelectedTreeNode = connectionInfo;
            Assert.That(_configWindow.VisibleObjectProperties, Is.EquivalentTo(expectedVisibleProperties));
        }

        [Test]
        public void PropertyGridShowCorrectPropertiesForRootConnectionInfo()
        {
            var expectedVisibleProperties = new[]
            {
                nameof(RootNodeInfo.Name),
                nameof(RootNodeInfo.Password),
            };

            _configWindow.SelectedTreeNode = new RootNodeInfo(RootNodeType.Connection);
            Assert.That(_configWindow.VisibleObjectProperties, Is.EquivalentTo(expectedVisibleProperties));
        }

        private static IEnumerable<TestCaseData> ConnectionInfoGeneralTestCases()
        {
            var testCases = new List<TestCaseData>();

            foreach (var protocol in ProtocolTypes.Supported)
            {
                foreach (var isContainer in new[] { false, true })
                {
                    var expectedProperties = BuildExpectedConnectionInfoPropertyList(protocol, isContainer);
                    var node = ConstructConnectionInfo(protocol, isContainer);
                    testCases.Add(new TestCaseData(node, expectedProperties)
                        .SetName(protocol + ", " + (isContainer ? "ContainerInfo" : "ConnectionInfo")));
                }
            }

            return testCases;
        }

        internal static ConnectionInfo ConstructConnectionInfo(ProtocolType protocol, bool isContainer)
        {
            // build connection info. set certain connection properties so
            // that toggled properties are hidden in the property grid. We
            // will test those separately in the special protocol tests.
            var node = isContainer
                ? new ContainerInfo()
                : new ConnectionInfo();

            node.Protocol = protocol;
            node.Resolution = RdpProtocol.RDPResolutions.Res800x600;
            node.RDGatewayUsageMethod = RdpProtocol.RDGatewayUsageMethod.Never;
            node.RDGatewayUseConnectionCredentials = RdpProtocol.RDGatewayUseConnectionCredentials.Yes;
            node.RedirectSound = RdpProtocol.RDPSounds.DoNotPlay;
            node.Inheritance.TurnOffInheritanceCompletely();

            return node;
        }

        internal static List<string> BuildExpectedConnectionInfoPropertyList(ProtocolType protocol, bool isContainer)
        {
            if (!ProtocolTypes.IsSupported(protocol))
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Protocol is not supported by this build.");

            var isPuttyProtocol = protocol != ProtocolType.RDP;
            var hidesCredentials = protocol == ProtocolType.Telnet ||
                                   protocol == ProtocolType.Rlogin ||
                                   protocol == ProtocolType.RAW;

            var expectedProperties = new List<string>
            {
                nameof(ConnectionInfo.Name),
                nameof(ConnectionInfo.Description),
                nameof(ConnectionInfo.Icon),
                nameof(ConnectionInfo.Panel),
                nameof(ConnectionInfo.Protocol),
                nameof(ConnectionInfo.MacAddress),
                nameof(ConnectionInfo.UserField),
            };

            if (!isContainer)
            {
                expectedProperties.Add(nameof(ConnectionInfo.Hostname));
            }

            if (!hidesCredentials)
            {
                expectedProperties.Add(nameof(ConnectionInfo.Username));
                expectedProperties.Add(nameof(ConnectionInfo.Password));
            }

            if (!isPuttyProtocol)
            {
                expectedProperties.Add(nameof(ConnectionInfo.Domain));
            }

            expectedProperties.Add(nameof(ConnectionInfo.Port));

            if (isPuttyProtocol)
            {
                expectedProperties.Add(nameof(ConnectionInfo.PuttySession));
                return expectedProperties;
            }

            expectedProperties.AddRange(new []
            {
                nameof(ConnectionInfo.UseConsoleSession),
                nameof(ConnectionInfo.RDPAuthenticationLevel),
                nameof(ConnectionInfo.RDPMinutesToIdleTimeout),
                nameof(ConnectionInfo.LoadBalanceInfo),
                nameof(ConnectionInfo.UseCredSsp),
                nameof(ConnectionInfo.RDGatewayUsageMethod),
                nameof(ConnectionInfo.Resolution),
                nameof(ConnectionInfo.Colors),
                nameof(ConnectionInfo.CacheBitmaps),
                nameof(ConnectionInfo.DisplayWallpaper),
                nameof(ConnectionInfo.DisplayThemes),
                nameof(ConnectionInfo.EnableFontSmoothing),
                nameof(ConnectionInfo.EnableDesktopComposition),
                nameof(ConnectionInfo.RedirectKeys),
                nameof(ConnectionInfo.RedirectDiskDrives),
                nameof(ConnectionInfo.RedirectPrinters),
                nameof(ConnectionInfo.RedirectPorts),
                nameof(ConnectionInfo.RedirectSmartCards),
                nameof(ConnectionInfo.RedirectClipboard),
                nameof(ConnectionInfo.RedirectSound),
            });

            return expectedProperties;
        }
    }
}
