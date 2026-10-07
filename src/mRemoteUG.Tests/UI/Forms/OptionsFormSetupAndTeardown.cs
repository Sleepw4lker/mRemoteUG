using NUnit.Framework;
using mRemoteUG.UI.Forms;

namespace mRemoteUG.Tests.UI.Forms
{
    public class OptionsFormSetupAndTeardown
    {
        protected OptionsForm _optionsForm;

        [OneTimeSetUp]
        public void OnetimeSetup()
        {
        }

        [SetUp]
        public void Setup()
        {
            _optionsForm = new OptionsForm();
            _optionsForm.Show();
        }

        [TearDown]
        public void Teardown()
        {
            _optionsForm.Dispose();
            while (_optionsForm.Disposing) ;
            _optionsForm = null;
        }
    }
}