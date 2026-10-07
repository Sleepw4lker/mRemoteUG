using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins the shape of the committed COM interop assemblies under <c>ThirdParty\Interop</c>.
    /// </summary>
    /// <remarks>
    /// <c>ThirdParty\Interop\README.md</c> and <c>Tools\regenerate-mstsc-interop.ps1</c> both tell
    /// you to run <c>dotnet test mRemoteUG.Tests --filter AxInteropSurface</c> after regenerating
    /// them. Until now that filter matched nothing.
    /// <para>
    /// Most member-presence facts are already enforced by compilation. What the compiler does
    /// <em>not</em> check - and what regenerating against a different <c>mstscax.tlb</c> could
    /// silently change - are the IIDs and the coclass wiring. Those are the load-bearing
    /// assertions here. Pure reflection: no ActiveX control, no STA, no COM activation, so this
    /// fixture runs on any machine.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class AxInteropSurfaceTests
    {
        /// <summary>IMsRdpClient10's IID, which v10, v11 and v12 all share as their default interface.</summary>
        private static readonly Guid IMsRdpClient10Iid = new Guid("7ed92c39-eb38-4927-a70a-708ac5a59321");

        /// <summary>The Microsoft Terminal Services Control type library that the script requests.</summary>
        private const string MstscTypeLibGuid = "8c11efa1-92c3-11d1-bc1e-00c04fa31489";

        [Test]
        public void TheV11AndV12CoclassesShareIMsRdpClient10sIid()
        {
            // This is why one managed type - MsRdpClient11NotSafeForScripting - drives both
            // coclasses: tlbimp gives a coclass interface the IID of the coclass's *default*
            // interface, not the CLSID. If this ever fails, RdpProtocol.Initialize's cast starts
            // throwing InvalidCastException against whichever coclass moved.
            Assert.Multiple(() =>
            {
                Assert.That(typeof(IMsRdpClient10).GUID, Is.EqualTo(IMsRdpClient10Iid));
                Assert.That(typeof(MsRdpClient11NotSafeForScripting).GUID, Is.EqualTo(IMsRdpClient10Iid),
                            "casting the OCX to MsRdpClient11NotSafeForScripting must be QueryInterface(IID_IMsRdpClient10).");
                Assert.That(typeof(MsRdpClient12NotSafeForScripting).GUID, Is.EqualTo(IMsRdpClient10Iid),
                            "a v12 control must answer the same QueryInterface as a v11 one.");
            });
        }

        [Test]
        public void TheV11CoclassInterfaceCarriesBothTheClientSurfaceAndTheEvents()
        {
            // RdpProtocol keeps a single _rdpClient field for both roles. If the event interface
            // were ever dropped from this type, every OnConnected/OnDisconnected subscription in
            // the teardown machinery would stop compiling - but the reason would be obscure.
            Assert.Multiple(() =>
            {
                Assert.That(typeof(MsRdpClient11NotSafeForScripting).IsInterface, Is.True);
                foreach (var required in new[]
                         {
                             typeof(IMsRdpClient10), typeof(IMsRdpClient9), typeof(IMsRdpClient8),
                             typeof(IMsTscAx), typeof(IMsTscAxEvents_Event)
                         })
                {
                    Assert.That(required.IsAssignableFrom(typeof(MsRdpClient11NotSafeForScripting)), Is.True,
                                $"MsRdpClient11NotSafeForScripting must extend {required.Name}.");
                }
            });
        }

        [Test]
        public void TheV12CoclassImplementsTheOptionalSideInterfaces()
        {
            // IMsRdpClient10 is the ceiling of the IMsRdpClientN chain - there is no
            // IMsRdpClient11 or 12 - so anything newer is only reachable through these.
            // RdpProtocol probes them for diagnostics rather than depending on them.
            var coclass = typeof(MsRdpClient12NotSafeForScriptingClass);
            Assert.Multiple(() =>
            {
                foreach (var side in new[]
                         {
                             typeof(IMsRdpClient10), typeof(IMsRdpClientNonScriptable8),
                             typeof(IMsRdpExtendedSettings), typeof(IMsRdpPreferredRedirectionInfo)
                         })
                {
                    Assert.That(side.IsAssignableFrom(coclass), Is.True,
                                $"MsRdpClient12NotSafeForScriptingClass must implement {side.Name}.");
                }

                Assert.That(typeof(MSTSCLib.IMsRdpClient10).Assembly.GetType("MSTSCLib.IMsRdpClient11"), Is.Null,
                            "the type library has no IMsRdpClient11; if one appears, RdpProtocol can be raised further.");
                Assert.That(typeof(MSTSCLib.IMsRdpClient10).Assembly.GetType("MSTSCLib.IMsRdpClient12"), Is.Null,
                            "the type library has no IMsRdpClient12; if one appears, RdpProtocol can be raised further.");
            });
        }

        [Test]
        public void TheAdvancedSettingsPropertiesAreOffByOneOnPurpose()
        {
            // AdvancedSettings8 is typed IMsRdpClientAdvancedSettings7, not ...8. This is a real
            // quirk of the type library and not a typo, and it is exactly the sort of thing
            // someone "corrects" while reading RdpProtocol.
            Assert.Multiple(() =>
            {
                Assert.That(typeof(IMsRdpClient10).GetProperty("AdvancedSettings8")?.PropertyType,
                            Is.EqualTo(typeof(IMsRdpClientAdvancedSettings7)));
                Assert.That(typeof(IMsRdpClient10).GetProperty("AdvancedSettings9")?.PropertyType,
                            Is.EqualTo(typeof(IMsRdpClientAdvancedSettings8)));
            });
        }

        [Test]
        public void EveryMemberRdpProtocolDrivesIsOnIMsRdpClient10()
        {
            // The point of the v11 retype: the whole protocol runs off this one interface.
            var client = typeof(IMsRdpClient10);
            Assert.Multiple(() =>
            {
                foreach (var property in new[]
                         {
                             "Server", "Domain", "UserName", "FullScreen", "FullScreenTitle", "ColorDepth",
                             "ConnectingText", "Connected", "DesktopWidth", "DesktopHeight", "Version",
                             "SecuredSettingsEnabled", "SecuredSettings2", "ExtendedDisconnectReason",
                             "AdvancedSettings2", "AdvancedSettings3", "AdvancedSettings5",
                             "AdvancedSettings7", "AdvancedSettings8", "TransportSettings", "TransportSettings2"
                         })
                {
                    Assert.That(client.GetProperty(property), Is.Not.Null, $"IMsRdpClient10.{property} is missing.");
                }

                foreach (var method in new[]
                         {
                             "Connect", "Disconnect", "RequestClose", "Reconnect",
                             "UpdateSessionDisplaySettings", "GetErrorDescription"
                         })
                {
                    Assert.That(client.GetMethod(method), Is.Not.Null, $"IMsRdpClient10.{method}() is missing.");
                }

                foreach (var name in new[]
                         {
                             "OnConnecting", "OnConnected", "OnLoginComplete", "OnDisconnected",
                             "OnFatalError", "OnLeaveFullScreenMode", "OnIdleTimeoutNotification", "OnConfirmClose"
                         })
                {
                    Assert.That(typeof(IMsTscAxEvents_Event).GetEvent(name), Is.Not.Null,
                                $"IMsTscAxEvents_Event.{name} is missing.");
                }
            });
        }

        /// <summary>
        /// Pins the shape of the one call that carries display scale to a session.
        /// </summary>
        /// <remarks>
        /// Worth pinning because the parameters are easy to get wrong and impossible to get told
        /// off for. <c>ulPhysicalWidth</c> and <c>ulPhysicalHeight</c> are millimetres, not pixels,
        /// and they sit between two pairs that *are* pixels; this fork passed the pixel size there
        /// for years, claiming a monitor about 1.9 metres wide. Nothing complained, because
        /// [MS-RDPEDISP] says an out-of-range value is ignored rather than rejected.
        /// </remarks>
        [Test]
        public void UpdateSessionDisplaySettingsHasTheSevenParametersWeThinkItHas()
        {
            var method = typeof(IMsRdpClient10).GetMethod("UpdateSessionDisplaySettings");
            Assert.That(method, Is.Not.Null);

            var parameters = method.GetParameters();
            Assert.Multiple(() =>
            {
                Assert.That(parameters.Select(p => p.Name), Is.EqualTo(new[]
                {
                    "ulDesktopWidth", "ulDesktopHeight",
                    "ulPhysicalWidth", "ulPhysicalHeight",
                    "ulOrientation",
                    "ulDesktopScaleFactor", "ulDeviceScaleFactor"
                }), "the parameter list changed - check which of these are pixels and which are millimetres.");

                Assert.That(parameters.Select(p => p.ParameterType),
                            Is.All.EqualTo(typeof(uint)));
            });
        }

        /// <summary>
        /// Pins the shape of the extended-settings property bag, which is how the display scale is
        /// declared before a session exists.
        /// </summary>
        /// <remarks>
        /// Two things about it are easy to get wrong and this is the only place that would notice.
        /// <para>
        /// The setter takes its value **by reference** - <c>set_Property(string, ref object)</c> -
        /// so the C# indexer does not bind and it has to be called as a method with an explicit
        /// <c>ref</c>. A regenerated interop that declared it by value would compile against a
        /// different call and silently change what <c>RdpProtocol.SetDisplayScale</c> means.
        /// </para>
        /// <para>
        /// The value is typed <c>object</c>, so nothing in the type system says which variant the
        /// bag wants. It wants VT_UI4: measured by <c>--selftest</c> on mstscax 10.0.26100, where a
        /// boxed <c>int</c> throws E_FAIL. That is a fact about mstscax rather than about the
        /// interop, so <c>CheckRdpControl</c> re-measures it on every run and this only pins the
        /// shape the measurement is made through.
        /// </para>
        /// </remarks>
        [Test]
        public void TheExtendedSettingsBagTakesItsValueByReference()
        {
            var setter = typeof(IMsRdpExtendedSettings).GetMethod("set_Property");
            var getter = typeof(IMsRdpExtendedSettings).GetMethod("get_Property");

            Assert.Multiple(() =>
            {
                Assert.That(setter, Is.Not.Null, "IMsRdpExtendedSettings has no set_Property.");
                Assert.That(getter, Is.Not.Null, "IMsRdpExtendedSettings has no get_Property.");

                var parameters = setter.GetParameters();
                Assert.That(parameters.Select(p => p.Name), Is.EqualTo(new[] { "bstrPropertyName", "pValue" }));
                Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));
                Assert.That(parameters[1].ParameterType.IsByRef, Is.True,
                            "set_Property no longer takes its value by ref, so the explicit ref in " +
                            "RdpProtocol.SetDisplayScale is now wrong.");
                Assert.That(parameters[1].ParameterType.GetElementType(), Is.EqualTo(typeof(object)),
                            "the bag is untyped on purpose - the variant it wants is measured by " +
                            "--selftest, not declared here.");
            });
        }

        [Test]
        public void TheAxWrappersForEveryCandidateExistAndAreAxHosts()
        {
            Assert.Multiple(() =>
            {
                foreach (var wrapper in new[]
                         {
                             typeof(AxMsRdpClient11NotSafeForScripting),
                             typeof(AxMsRdpClient12NotSafeForScripting)
                         })
                {
                    Assert.That(typeof(AxHost).IsAssignableFrom(wrapper), Is.True,
                                $"{wrapper.Name} must derive from AxHost.");
                }
            });
        }

        [Test]
        public void TheInteropWasGeneratedFromTheMstscTypeLibrary()
        {
            // Pins the identity that Tools\regenerate-mstsc-interop.ps1 asks for, so interop
            // generated from something else cannot be committed unnoticed.
            var guid = typeof(IMsRdpClient10).Assembly.GetCustomAttribute<GuidAttribute>();
            Assert.That(guid, Is.Not.Null, "Interop.MSTSCLib.dll carries no GuidAttribute.");
            Assert.That(guid.Value, Is.EqualTo(MstscTypeLibGuid).IgnoreCase);
        }
    }
}
