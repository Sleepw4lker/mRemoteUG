using System;
using System.Drawing;
using System.Windows.Forms;

namespace mRemoteUG.UI.Forms
{
    public class FullscreenHandler
    {
        private readonly Form _handledForm;
        private FormWindowState _savedWindowState;
        private FormBorderStyle _savedBorderStyle;
        private Rectangle _savedBounds;
        private int _savedDpi;
        private bool _value;

        public bool Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (_value == value) return;
                if (!_value)
                    EnterFullscreen();
                else
                    ExitFullscreen();
                _value = value;
            }
        }

        public FullscreenHandler(Form handledForm)
        {
            _handledForm = handledForm;
        }

        private void EnterFullscreen()
        {
            _savedBorderStyle = _handledForm.FormBorderStyle;
            _savedWindowState = _handledForm.WindowState;
            _savedBounds = _handledForm.Bounds;
            _savedDpi = _handledForm.DeviceDpi;

            _handledForm.FormBorderStyle = FormBorderStyle.None;
            if (_handledForm.WindowState == FormWindowState.Maximized)
            {
                _handledForm.WindowState = FormWindowState.Normal;
            }
            _handledForm.WindowState = FormWindowState.Maximized;
        }
        private void ExitFullscreen()
        {
            _handledForm.FormBorderStyle = _savedBorderStyle;
            _handledForm.WindowState = _savedWindowState;
            _handledForm.Bounds = RestoredBounds();
        }

        /// <summary>
        /// The bounds saved on the way in, corrected if the DPI has changed since.
        /// </summary>
        /// <remarks>
        /// Going full screen is exactly how a window ends up on a different monitor: maximising
        /// onto a 4K panel and coming back restores a size measured in the old monitor's pixels.
        /// The size is a measurement and scales; the location is a desktop coordinate and does not.
        /// </remarks>
        private Rectangle RestoredBounds()
        {
            var currentDpi = _handledForm.DeviceDpi;
            if (_savedDpi <= 0 || _savedDpi == currentDpi)
                return _savedBounds;

            return new Rectangle(
                _savedBounds.Location,
                new Size((int)Math.Round(_savedBounds.Width * currentDpi / (double)_savedDpi),
                         (int)Math.Round(_savedBounds.Height * currentDpi / (double)_savedDpi)));
        }
    }
}