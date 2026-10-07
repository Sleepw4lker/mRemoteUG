/* 
 * http://www.csharp-examples.net/inputbox/ 
 * 
 */
using System;
using System.Windows.Forms;
using System.Drawing;

namespace mRemoteUG.UI.Forms.Input
{
    internal static class input
    {
        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            var form = new Form();
            // Built in code rather than by the designer, so there is no InitializeComponent/
            // SuspendLayout/ResumeLayout(false)/PerformLayout() sequence to carry the ordinary
            // AutoScaleMode.Font pass. Measured directly (create this exact control graph with
            // SetThreadDpiAwarenessContext forcing 192 DPI, then compare Bounds before/after):
            // declaring AutoScaleDimensions/AutoScaleMode and even forcing a PerformLayout() before
            // Show() never scales anything here - ClientSize and every child Bounds come out
            // pixel-identical to the 96 DPI case, which is what clipped the OK/Cancel buttons
            // against the window edge on a HiDPI display. Scaling through LogicalToDeviceUnits
            // instead is driven by DeviceDpi directly rather than the Font-based pass, so it is not
            // subject to whatever is missing from that sequence here. The literals below are 96 DPI
            // measurements.
            int Scale(int value) => form.LogicalToDeviceUnits(value);

            var label = new System.Windows.Forms.Label();
            var textBox = new System.Windows.Forms.TextBox();
            var buttonOk = new System.Windows.Forms.Button();
            var buttonCancel = new System.Windows.Forms.Button();

            label.Text = promptText;
            label.AutoSize = true;
            label.SetBounds(Scale(9), Scale(20), Scale(372), Scale(13));

            textBox.Text = value;
            textBox.BorderStyle = BorderStyle.Fixed3D;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            textBox.SetBounds(Scale(12), Scale(36), Scale(372), Scale(20));

            buttonOk.Text = Language.strButtonOK;
            buttonOk.DialogResult = DialogResult.OK;
            buttonOk.FlatStyle = FlatStyle.Flat;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonOk.SetBounds(Scale(228), Scale(72), Scale(75), Scale(23));

            buttonCancel.Text = Language.strButtonCancel;
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.FlatStyle = FlatStyle.Flat;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.SetBounds(Scale(309), Scale(72), Scale(75), Scale(23));

            form.Text = title;
            form.ClientSize = new Size(Scale(396), Scale(107));
            form.Controls.AddRange(new Control[] {label, textBox, buttonOk, buttonCancel});
            form.ClientSize = new Size(Math.Max(Scale(300), label.Right + Scale(10)), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            var dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
    }
}

