using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Controls;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Controls
{
    /// <summary>
    /// The quick connect box must not recreate its window when its font changes.
    /// </summary>
    /// <remarks>
    /// WinForms scales a control's font from inside <c>WM_DPICHANGED_BEFOREPARENT</c> whenever that
    /// control has an explicitly set font - and <c>ToolStripControlHost</c> gives every hosted
    /// control one. A <see cref="ComboBox"/> whose <c>AutoCompleteMode</c> is set responds to a font
    /// change by recreating its window, so moving the main window to a monitor of a different scale
    /// meant recreating a window from inside that window's own message handler. On a move from a
    /// 200% monitor to a 300% one, <c>CreateWindowEx</c> there failed with Win32 1400,
    /// <c>ERROR_INVALID_WINDOW_HANDLE</c>, and the process went down. See ADR-0029.
    /// <para>
    /// This pins the mechanism, not the crash. The crash needs a real move between real monitors,
    /// which neither this test host nor <c>--selftest</c> can stage; what both can check is that the
    /// window recreation the crash happened inside no longer occurs. The selftest has the same check
    /// against a real per-monitor-aware process, because WinForms only takes the font-scaling path
    /// there at all - which is why this one drives the font directly rather than a DPI message.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class QuickConnectComboBoxDpiTests
    {
        private Form _form;
        private QuickConnectToolStrip _strip;
        private ComboBox _hosted;

        [SetUp]
        public void Setup()
        {
            _form = new Form { ShowInTaskbar = false, Width = 900, Height = 200 };
            _strip = new QuickConnectToolStrip();
            _form.Controls.Add(_strip);
            _form.Show();
            Application.DoEvents();
            _hosted = _strip.Items.OfType<QuickConnectComboBox>().Single().ComboBox;
        }

        [TearDown]
        public void TearDown()
        {
            _form?.Dispose();
            _form = null;
        }

        [Test]
        public void AFontChangeDoesNotRecreateTheHostedWindow()
        {
            Assert.That(_hosted.IsHandleCreated, Is.True,
                        "no window to recreate, so this test proves nothing.");

            var before = _hosted.Handle;
            _hosted.Font = new Font(_hosted.Font.FontFamily, _hosted.Font.Size * 1.5f);

            Assert.That(_hosted.Handle, Is.EqualTo(before),
                        "The quick connect box recreated its window for a font change. WinForms does " +
                        "that scaling inside WM_DPICHANGED_BEFOREPARENT, so this is the 200%-to-300% " +
                        "crash coming back.");
        }

        /// <summary>
        /// Names the cause, so that a later change that reintroduces it fails for a reason rather
        /// than as a mysterious handle comparison.
        /// </summary>
        [Test]
        public void TheQuickConnectBoxHasNoAutocomplete()
        {
            var item = _strip.Items.OfType<QuickConnectComboBox>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(item.AutoCompleteMode, Is.EqualTo(AutoCompleteMode.None),
                            "Autocomplete is what makes a ComboBox recreate its window on a font " +
                            "change; see ADR-0029 before turning it back on.");
                Assert.That(item.AutoCompleteSource, Is.EqualTo(AutoCompleteSource.None));
            });
        }

        /// <summary>
        /// The half of the behaviour that was kept, so the fix is not silently widened into
        /// "the drop-down does nothing".
        /// </summary>
        [Test]
        public void TheDropDownStillHoldsItsItems()
        {
            _hosted.Items.Add("server1");
            _hosted.Items.Add("server2");

            Assert.That(_hosted.Items, Has.Count.EqualTo(2));
        }
    }
}
