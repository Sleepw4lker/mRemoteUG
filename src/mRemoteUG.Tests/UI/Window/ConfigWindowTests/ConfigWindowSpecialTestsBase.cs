using System.Collections.Generic;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests
{
    public abstract class ConfigWindowSpecialTestsBase
    {
        protected abstract ProtocolType Protocol { get; }
        protected bool TestAgainstContainerInfo { get; set; } = false;
        protected ConfigWindow ConfigWindow;
        protected ConnectionInfo ConnectionInfo;
        protected List<string> ExpectedPropertyList;

        [SetUp]
        public virtual void Setup()
        {
            ConnectionInfo = ConfigWindowGeneralTests.ConstructConnectionInfo(Protocol, TestAgainstContainerInfo);
            ExpectedPropertyList = ConfigWindowGeneralTests.BuildExpectedConnectionInfoPropertyList(Protocol, TestAgainstContainerInfo);

            ConfigWindow = new ConfigWindow
            {
                PropertiesVisible = true,
            };
        }

        public void RunVerification()
        {
            ConfigWindow.SelectedTreeNode = ConnectionInfo;
            Assert.That(
                ConfigWindow.VisibleObjectProperties,
                Is.EquivalentTo(ExpectedPropertyList));
        }
    }
}
