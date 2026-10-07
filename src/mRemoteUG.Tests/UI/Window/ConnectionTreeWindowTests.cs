using System.Threading;
using mRemoteUG.UI.Window;
using NUnit.Framework;


namespace mRemoteUG.Tests.UI.Window
{
    public class ConnectionTreeWindowTests
    {
        private ConnectionTreeWindow _connectionTreeWindow;

        [SetUp]
        public void Setup()
        {
            _connectionTreeWindow = new ConnectionTreeWindow();
        }

        [TearDown]
        public void Teardown()
        {
            _connectionTreeWindow.Close();
        }

        [Test, Apartment(ApartmentState.STA)]
        public void CanShowWindow()
        {
            _connectionTreeWindow.Show();
            Assert.That(_connectionTreeWindow.Visible);
        }
    }
}