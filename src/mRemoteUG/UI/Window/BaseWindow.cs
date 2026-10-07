using System;
using System.Windows.Forms;
using System.ComponentModel;
// ReSharper disable UnusedAutoPropertyAccessor.Global


namespace mRemoteUG.UI.Window
{
	public class BaseWindow : Form
    {
        private string _tabText = string.Empty;

        /// <summary>
        /// Deliberately does not set <see cref="ContainerControl.AutoScaleMode"/>.
        /// </summary>
        /// <remarks>
        /// Every derived window sets its own mode and baseline in its own InitializeComponent,
        /// because they do not share a font: four of them set Segoe UI 8.25pt (baseline 6x13)
        /// and the rest keep the ambient Segoe UI 9pt (baseline 7x15).
        /// <para>
        /// Setting the mode here instead looks tidier and is wrong. Assigning AutoScaleMode
        /// computes and caches CurrentAutoScaleDimensions, and a base constructor runs before
        /// the derived InitializeComponent has set the font - so the cache holds the ambient
        /// font's metric, the derived font never replaces it, and the window is scaled by a
        /// factor measured against a font it does not use. Measured, not theorised: it made the
        /// four 8.25pt windows 5% too tall, scaling their height by 20/13 rather than 19/13.
        /// </para>
        /// </remarks>
        public BaseWindow()
        {
        }

        #region Public Properties

        /// <summary>
        /// Caption shown for this window on its tab in the document area, and on the
        /// View menu. Kept separate from Text because a connection panel can be renamed
        /// independently of the window title.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TabText
        {
            get => _tabText;
            set
            {
                if (_tabText == value)
                    return;
                _tabText = value;
                TabTextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raised when TabText changes, so the host can retitle the tab.
        /// </summary>
        public event EventHandler TabTextChanged;

        /// <summary>
        /// When true, a user-initiated close hides the window instead of disposing it.
        /// The singleton tool windows rely on this to survive being closed and then
        /// re-shown from the View menu.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HideOnClose { get; set; }

        #endregion

        #region Public Methods
		public void SetFormText(string t)
		{
			Text = t;
			TabText = t;
		}
        #endregion

        #region Private Methods
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (HideOnClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }
        #endregion
	}
}
