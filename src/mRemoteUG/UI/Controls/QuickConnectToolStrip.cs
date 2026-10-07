using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Tools;

namespace mRemoteUG.UI.Controls
{
    public class QuickConnectToolStrip : ToolStrip
    {
        private IContainer components;
        private ToolStripLabel _lblQuickConnect;
        private ToolStripDropDownButton _btnConnections;
        private ToolStripSplitButton _btnQuickConnect;
        private ContextMenuStrip _mnuQuickConnectProtocol;
        private QuickConnectComboBox _cmbQuickConnect;
        private ContextMenuStrip _mnuConnections;
        private IConnectionInitiator _connectionInitiator = new ConnectionInitiator();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IConnectionInitiator ConnectionInitiator
        {
            get { return _connectionInitiator; }
            set
            {
                if (value == null)
                    return;
                _connectionInitiator = value;
            }
        }

        public QuickConnectToolStrip()
        {
            Initialize();
            PopulateQuickConnectProtocolMenu();
        }

        private void Initialize()
        {
            components = new System.ComponentModel.Container();
            _lblQuickConnect = new ToolStripLabel();
            _cmbQuickConnect = new QuickConnectComboBox();
            _btnQuickConnect = new ToolStripSplitButton();
            _mnuQuickConnectProtocol = new ContextMenuStrip(components);
            _btnConnections = new ToolStripDropDownButton();
            _mnuConnections = new ContextMenuStrip(components);
            SuspendLayout();
            //
            // lblQuickConnect
            // 
            _lblQuickConnect.Name = "lblQuickConnect";
            _lblQuickConnect.Text = Language.strLabelConnect;
            _lblQuickConnect.Click += lblQuickConnect_Click;
            // 
            // cmbQuickConnect
            // 
            // No autocomplete, and this is not a style preference: it crashes the application.
            //
            // Measured. A ComboBox with AutoCompleteMode set recreates its window whenever its
            // font changes, and one with it None does not - the same font change, the same control,
            // the handle kept. WinForms scales this control's font from inside
            // WM_DPICHANGED_BEFOREPARENT, because ToolStripControlHost gives every hosted control
            // an explicitly set font, so a monitor with a different scale means a window
            // recreation inside that message. On a move from a 200% monitor to a 300% one,
            // CreateWindowEx there failed with Win32 1400 and took the process with it.
            //
            // The list itself is unaffected: the drop-down still holds the connection history and
            // still opens. What is gone is suggest-as-you-type. See ADR-0029, and the
            // "DPI change on hosted combo boxes" check in SelfTest, which fails if anything gives
            // a hosted combo box a reason to recreate its window again.
            _cmbQuickConnect.AutoCompleteMode = AutoCompleteMode.None;
            _cmbQuickConnect.AutoCompleteSource = AutoCompleteSource.None;
            _cmbQuickConnect.Name = "cmbQuickConnect";
            _cmbQuickConnect.ConnectRequested += cmbQuickConnect_ConnectRequested;
            _cmbQuickConnect.ProtocolChanged += cmbQuickConnect_ProtocolChanged;
            // 
            // tsQuickConnect
            // 
            Dock = DockStyle.None;
            Items.AddRange(new ToolStripItem[] {
            _lblQuickConnect,
            _cmbQuickConnect,
            _btnQuickConnect,
            _btnConnections});
            // No MaximumSize. A 25 pixel height cap is a 96 DPI measurement, and MaximumSize is
            // not touched by the auto-scale pass, so above 100% it clipped the combo box and the
            // buttons it is meant to contain. A ToolStrip auto-sizes to its items, which is the
            // DPI-correct height at any scale.
            //
            // No pixel sizes on the items either, and none on this strip. Every item here has the
            // default AutoSize, so the sizes that used to sit in this block measured nothing - the
            // strip's own Size and Location were overwritten by the designer and then again by
            // ToolStripPanel.Join. Dead 96 DPI numbers sitting beside the one live one are what
            // hid this defect: the entry field's width was the only size that did anything, and it
            // did the wrong thing. ApplyEntryFieldMetrics below is now the only size in the file.
            Name = "tsQuickConnect";
            TabIndex = 18;
            // 
            // btnQuickConnect
            // 
            _btnQuickConnect.DropDown = _mnuQuickConnectProtocol;
            _btnQuickConnect.Image = Resources.Play_Quick;
            _btnQuickConnect.ImageTransparentColor = System.Drawing.Color.Magenta;
            _btnQuickConnect.Name = "btnQuickConnect";
            _btnQuickConnect.Text = Language.strMenuConnect;
            _btnQuickConnect.ButtonClick += btnQuickConnect_ButtonClick;
            _btnQuickConnect.DropDownItemClicked += btnQuickConnect_DropDownItemClicked;
            // 
            // mnuQuickConnectProtocol
            // 
            _mnuQuickConnectProtocol.Name = "mnuQuickConnectProtocol";
            _mnuQuickConnectProtocol.OwnerItem = _btnQuickConnect;
            _mnuQuickConnectProtocol.ShowCheckMargin = true;
            _mnuQuickConnectProtocol.ShowImageMargin = false;
            // 
            // btnConnections
            // 
            _btnConnections.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnConnections.DropDown = _mnuConnections;
            _btnConnections.Image = Resources.Root;
            _btnConnections.ImageTransparentColor = System.Drawing.Color.Magenta;
            _btnConnections.Name = "btnConnections";
            _btnConnections.Text = Language.strMenuConnections;
            _btnConnections.DropDownOpening += btnConnections_DropDownOpening;
            // 
            // mnuConnections
            // 
            _mnuConnections.Name = "mnuConnections";
            _mnuConnections.OwnerItem = _btnConnections;

            ApplyEntryFieldMetrics(DeviceDpi);

            ResumeLayout();
        }

        /// <summary>
        /// The width the entry field needs at a DPI, and the margins around it.
        /// </summary>
        /// <remarks>
        /// The field used to be a flat <c>Size = (200, 25)</c>, which held at 96 DPI and nowhere
        /// else. A <c>ToolStripItem</c> is not a <c>Control</c>, so the auto-scale pass does not
        /// reach its Size the way it reaches a control's bounds - the same fact already recorded
        /// for <c>ColumnHeader.Width</c> in docs/platform-findings.md - and on a
        /// <see cref="ToolStripComboBox"/> that turns into a self-sustaining value: assigning the
        /// item's Size pushes the width onto the hosted ComboBox, and
        /// <c>ToolStripControlHost.GetPreferredSize</c> reads it straight back out. Measured, the
        /// field sat at 200 physical pixels from 96 DPI to 192 while the font inside it doubled.
        /// <para>
        /// Two axes have to be satisfied at once, and they disagree. A DPI-scaled literal is blind
        /// to a larger system font at unchanged DPI; a font measurement alone is blind to the DPI
        /// where the font does not carry it. So the width is the larger of the two, which is the
        /// same shape as the connection tree's search box.
        /// </para>
        /// <para>
        /// Every term is read afresh from the current font and the DPI passed in, and nothing reads
        /// the property it is about to write. That is deliberate and it is load-bearing: measured,
        /// an auto-scale pass over this strip takes the field from 200 pixels to 396, so a width
        /// derived from its own previous value would compound on every pass. Recomputing an
        /// absolute value cannot, however many passes run and in whatever order.
        /// </para>
        /// </remarks>
        private void ApplyEntryFieldMetrics(int dpi)
        {
            // Reached from OnFontChanged, which a ToolStrip can raise before Initialize has built
            // the items it is about to measure.
            if (_cmbQuickConnect == null || _btnQuickConnect == null)
                return;

            // A ToolStripComboBox does not get its font from the strip it sits on. Measured: the
            // strip went to 18pt and the hosted ComboBox stayed at 9pt, because
            // ToolStripControlHost.Font *is* the hosted control's font rather than something
            // inherited from the owner. So DpiScaling.FollowDpiChange grew the menu text and left
            // the entry field's text where it was; it has to be handed down by name. Assigned,
            // not copied, for the reason FollowDpiChange gives: a control never disposes a font it
            // was handed, so sharing the window's instance leaves nothing to clean up.
            var font = Font;
            if (_cmbQuickConnect.ComboBox != null && !font.Equals(_cmbQuickConnect.ComboBox.Font))
                _cmbQuickConnect.ComboBox.Font = font;

            var measured = System.Windows.Forms.TextRenderer
                               .MeasureText(new string('0', EntryCharacters), font).Width
                         + DpiScaling.Scale(LogicalComboChrome, dpi);

            _cmbQuickConnect.Margin = DpiScaling.Scale(new Padding(1, 0, 3, 0), dpi);
            _btnQuickConnect.Margin = DpiScaling.Scale(new Padding(0, 1, 3, 2), dpi);

            _cmbQuickConnect.Size =
                new System.Drawing.Size(Math.Max(DpiScaling.Scale(LogicalEntryWidth, dpi), measured),
                                        _cmbQuickConnect.ComboBox?.PreferredHeight
                                            ?? _cmbQuickConnect.Height);
        }

        /// <summary>What the entry field is meant to hold: a long FQDN and a five digit port.</summary>
        private const int EntryCharacters = 35;

        /// <summary>The same field at 96 DPI, used as the floor when the font measures narrower.</summary>
        private const int LogicalEntryWidth = 280;

        /// <summary>
        /// The drop-down button and text inset, at 96 DPI.
        /// </summary>
        /// <remarks>
        /// Not <see cref="SystemInformation.VerticalScrollBarWidth"/>, which wraps
        /// <c>GetSystemMetrics</c> and so answers for the system DPI rather than this monitor's.
        /// </remarks>
        private const int LogicalComboChrome = 17;

        /// <summary>
        /// Re-measures the entry field, from the window's DPI follow-up pass.
        /// </summary>
        /// <remarks>
        /// The same arrangement as <c>ConnectionWindow.RefreshTabMetrics</c>, and for the same
        /// reason: the pass is posted, so it runs after the framework has finished whatever
        /// scaling it intends to do, and it carries the DPI it settled on rather than leaving this
        /// strip to infer one.
        /// </remarks>
        internal void RefreshEntryFieldMetrics(int dpi)
        {
            if (IsDisposed)
                return;

            ApplyEntryFieldMetrics(dpi);
        }

        /// <summary>
        /// The font is how a DPI change reaches this strip.
        /// </summary>
        /// <remarks>
        /// A ToolStrip does not inherit its parent's font, so <c>DpiScaling.FollowDpiChange</c>
        /// assigns the window's font to every strip on a DPI change - which lands here. It is also
        /// the only notification a larger system font gives, the DPI never having changed.
        /// <para>
        /// <c>base</c> first: it is the base implementation that pushes the new font down to the
        /// hosted ComboBox, so measuring before it would measure the font being replaced.
        /// </para>
        /// </remarks>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ApplyEntryFieldMetrics(DeviceDpi);
        }

        /// <summary>
        /// The DPI hook a strip inside a window actually receives.
        /// </summary>
        /// <remarks>
        /// The DPI comes from the argument rather than from <see cref="Control.DeviceDpi"/>, which
        /// is not necessarily the new value yet while the framework is part way through its own
        /// relayout.
        /// </remarks>
        protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
        {
            base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
            ApplyEntryFieldMetrics(deviceDpiNew);
        }

        #region Quick Connect
        private void PopulateQuickConnectProtocolMenu()
        {
            try
            {
                _mnuQuickConnectProtocol.Items.Clear();
                foreach (var protocol in ProtocolTypes.Supported)
                {
                    var name = protocol.ToString();
                    var menuItem = new ToolStripMenuItem(name);
                    if (name == Settings.Default.QuickConnectProtocol)
                    {
                        menuItem.Checked = true;
                        _btnQuickConnect.Text = Settings.Default.QuickConnectProtocol;
                    }
                    _mnuQuickConnectProtocol.Items.Add(menuItem);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("PopulateQuickConnectProtocolMenu() failed.", ex);
            }
        }

        private void lblQuickConnect_Click(object? sender, EventArgs e)
        {
            _cmbQuickConnect.Focus();
        }

        private void cmbQuickConnect_ConnectRequested(object sender, QuickConnectComboBox.ConnectRequestedEventArgs e)
        {
            btnQuickConnect_ButtonClick(sender, e);
        }

        private void btnQuickConnect_ButtonClick(object? sender, EventArgs e)
        {
            try
            {
                var connectionInfo = Runtime.ConnectionsService.CreateQuickConnect(_cmbQuickConnect.Text.Trim(), Converter.StringToProtocol(Settings.Default.QuickConnectProtocol));
                if (connectionInfo == null)
                {
                    _cmbQuickConnect.Focus();
                    return;
                }
                _cmbQuickConnect.Add(connectionInfo);
                ConnectionInitiator.OpenConnection(connectionInfo, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("btnQuickConnect_ButtonClick() failed.", ex);
            }
        }

        private void cmbQuickConnect_ProtocolChanged(object sender, QuickConnectComboBox.ProtocolChangedEventArgs e)
        {
            SetQuickConnectProtocol(Converter.ProtocolToString(e.Protocol));
        }

        private void btnQuickConnect_DropDownItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            SetQuickConnectProtocol(e.ClickedItem.Text);
            btnQuickConnect_ButtonClick(this, e);
        }

        private void SetQuickConnectProtocol(string protocol)
        {
            Settings.Default.QuickConnectProtocol = protocol;
            _btnQuickConnect.Text = protocol;
            foreach (ToolStripMenuItem menuItem in _mnuQuickConnectProtocol.Items)
            {
                menuItem.Checked = menuItem.Text.Equals(protocol);
            }
        }
        #endregion

        #region Connections DropDown
        private void btnConnections_DropDownOpening(object? sender, EventArgs e)
        {
            _btnConnections.DropDownItems.Clear();
            var menuItemsConverter = new ConnectionsTreeToMenuItemsConverter
            {
                MouseUpEventHandler = ConnectionsMenuItem_MouseUp
            };

            // ReSharper disable once CoVariantArrayConversion
            ToolStripItem[] rootMenuItems = menuItemsConverter.CreateToolStripDropDownItems(Runtime.ConnectionsService.ConnectionTreeModel).ToArray();
            _btnConnections.DropDownItems.AddRange(rootMenuItems);

        }

        private void ConnectionsMenuItem_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var menuItem = (ToolStripMenuItem) sender;

            // While we can connect to a whole folder at once, it is
            // probably not the expected behavior when navigating through
            // a nested menu. Just return
            var containerInfo = menuItem.Tag as ContainerInfo;
            if (containerInfo != null)
                return;

            var connectionInfo = menuItem.Tag as ConnectionInfo;
            if (connectionInfo != null)
            {
                ConnectionInitiator.OpenConnection(connectionInfo);
            }
        }
        #endregion

        // CodeAyalysis doesn't like null propagation
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "components")]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                components?.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
