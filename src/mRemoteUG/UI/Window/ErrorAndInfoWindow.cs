using System;
using System.Drawing;
using System.Collections;
using System.Globalization;
using System.Windows.Forms;
using System.Text;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.UI.Forms;
using System.ComponentModel;

namespace mRemoteUG.UI.Window
{
	public partial class ErrorAndInfoWindow : BaseWindow
	{
        private ControlLayout _layout = ControlLayout.Vertical;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Form PreviousActiveForm { get; set; }

        public ErrorAndInfoWindow()
        {
            InitializeComponent();
            ApplyDerivedFonts();
            LayoutVertical();
            FillImageList();
        }

        #region Form Stuff
        private void ErrorsAndInfos_Load(object sender, EventArgs e)
		{
			ApplyLanguage();
		}
				
		private void ApplyLanguage()
		{
			clmMessage.Text = Language.strColumnMessage;
			cMenMCCopy.Text = Language.strMenuNotificationsCopyAll;
			cMenMCDelete.Text = Language.strMenuNotificationsDeleteAll;
			TabText = Language.strMenuNotifications;
			Text = Language.strMenuNotifications;
		}
        #endregion
				
        #region Private Methods

		/// <summary>
		/// Rebuilds the one font this window derives from its own.
		/// </summary>
		/// <remarks>
		/// The message date is italic; everything else uses the window font, which is whatever
		/// Windows says the UI font is. A derived font is an explicitly set font and so opts its
		/// control out of inheriting the parent's, which means it has to be rebuilt whenever the
		/// font it came from changes - on a DPI change, and when the user changes their font.
		/// </remarks>
		private void ApplyDerivedFonts()
		{
			var italic = new Font(Font, FontStyle.Italic);
			var previous = _dateFont;
			lblMsgDate.Font = italic;

			// Whichever instance the label actually kept, which is not always the one just handed
			// to it: Control.Font keeps the font it already holds when the new one compares equal.
			// Measured - assign an equal font, read back, and the original instance is still there.
			// So disposing on the assumption that the new one won a rebuild frees the font the
			// label is using, and the next read of it throws ArgumentException.
			_dateFont = lblMsgDate.Font;

			if (previous != null && !ReferenceEquals(previous, _dateFont))
				previous.Dispose();

			if (!ReferenceEquals(italic, _dateFont))
				italic.Dispose();
		}

		private Font _dateFont;

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			ApplyDerivedFonts();
		}

        /// <summary>
		/// Fills the severity list, in MessageClass order.
		/// </summary>
		/// <remarks>
		/// Added with keys as well as in order. The list is read by index -
		/// NotificationMessageListViewItem sets ImageIndex from the MessageClass - but a DPI change
		/// rebuilds it from its keys, and an image added without one cannot be resolved again.
		/// </remarks>
		private void FillImageList()
		{
		    imgListMC.Images.Add("brick", Resources.brick);
			imgListMC.Images.Add("InformationSmall", Resources.InformationSmall);
			imgListMC.Images.Add("WarningSmall", Resources.WarningSmall);
			imgListMC.Images.Add("ErrorSmall", Resources.ErrorSmall);
		}
				
		/// <summary>
		/// Keeps the severity icons in step with the DPI.
		/// </summary>
		/// <remarks>
		/// The list is re-assigned, not merely resized: a ListView re-measures its rows only
		/// when the property is set. The four images are re-resolved at the new size rather than
		/// stretched, which is what the artwork gaining a ladder of frames bought.
		/// </remarks>
		protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
		{
			base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
			UI.DpiScaling.ResizeForDpi(imgListMC, 16, deviceDpiNew, UI.Glyphs.Get);
			lvErrorCollector.SmallImageList = imgListMC;
		}

		/// <summary>
		/// This window lays itself out in code on every resize, so none of the numbers it uses
		/// are reached by the auto-scale pass and each has to be scaled where it is used.
		/// </summary>
		private int Dp(int logical) => LogicalToDeviceUnits(logical);

		private void LayoutVertical()
		{
			try
			{
				pnlErrorMsg.Location = new Point(0, Height - Dp(200));
				pnlErrorMsg.Size = new Size(Width, Height - pnlErrorMsg.Top);
				pnlErrorMsg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
				txtMsgText.Size = new Size(pnlErrorMsg.Width - pbError.Width - Dp(8), pnlErrorMsg.Height - Dp(20));
				lvErrorCollector.Location = new Point(0, 0);
				lvErrorCollector.Size = new Size(Width, Height - pnlErrorMsg.Height - Dp(5));
				lvErrorCollector.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
						
				_layout = ControlLayout.Vertical;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "LayoutVertical (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		private void LayoutHorizontal()
		{
			try
			{
				pnlErrorMsg.Location = new Point(0, 0);
				pnlErrorMsg.Size = new Size(Dp(200), Height);
				pnlErrorMsg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Top;
				txtMsgText.Size = new Size(pnlErrorMsg.Width - pbError.Width - Dp(8), pnlErrorMsg.Height - Dp(20));
				lvErrorCollector.Location = new Point(pnlErrorMsg.Width + Dp(5), 0);
				lvErrorCollector.Size = new Size(Width - pnlErrorMsg.Width - Dp(5), Height);
				lvErrorCollector.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
						
				_layout = ControlLayout.Horizontal;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "LayoutHorizontal (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		private void ErrorsAndInfos_Resize(object sender, EventArgs e)
		{
			try
			{
				if (Width > Height)
				{
					if (_layout == ControlLayout.Vertical)
						LayoutHorizontal();
				}
				else
				{
					if (_layout == ControlLayout.Horizontal)
						LayoutVertical();
				}
						
				lvErrorCollector.Columns[0].Width = lvErrorCollector.Width - Dp(20);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "ErrorsAndInfos_Resize (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		/// <summary>
		/// Empties the detail pane, for when nothing is selected.
		/// </summary>
		/// <remarks>
		/// This used to restore two background colours as well, because each severity painted
		/// the pane a colour of its own. It never restored the foreground, so the black text a
		/// warning or an error set stayed behind for the next message to inherit. The severity
		/// icon says the same thing without hardcoding a palette, so the colours are gone and
		/// the pane now follows whatever theme the application is running in.
		/// </remarks>
		private void ClearMessageDetails()
		{
			try
			{
				pbError.Image = null;
				txtMsgText.Text = "";
				lblMsgDate.Text = "";
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "ClearMessageDetails (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		private void MC_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
			    if (e.KeyCode != Keys.Escape) return;
			    try
			    {
			        // Escape dismisses the panel and hands focus back to wherever it came from
			        FrmMain.Default.Layout.HideTool(this);
			        if (PreviousActiveForm != null)
			            PreviousActiveForm.Focus();
			        else
			            FrmMain.Default.Layout.ShowTool(AppWindows.TreeForm);
			    }
			    catch (Exception)
			    {
			        // ignored
			    }
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "MC_KeyDown (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		private void lvErrorCollector_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (lvErrorCollector.SelectedItems.Count == 0 | lvErrorCollector.SelectedItems.Count > 1)
				{
					ClearMessageDetails();
					return;
				}
						
				var sItem = lvErrorCollector.SelectedItems[0];
                var eMsg = (Messages.Message)sItem.Tag;
				switch (eMsg.Class)
				{
                    case MessageClass.DebugMsg:
				        pbError.Image = Resources.brick;
                        break;
					case MessageClass.InformationMsg:
						pbError.Image = Resources.Information;
                        break;
					case MessageClass.WarningMsg:
						pbError.Image = Resources.Warning;
                        break;
					case MessageClass.ErrorMsg:
						pbError.Image = Resources.Error;
                        break;
				}
						
				lblMsgDate.Text = eMsg.Date.ToString(CultureInfo.InvariantCulture);
				txtMsgText.Text = eMsg.Text;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "lvErrorCollector_SelectedIndexChanged (UI.Window.ErrorsAndInfos) failed" + Environment.NewLine + ex.Message, true);
			}
		}
				
		private void cMenMC_Opening(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (lvErrorCollector.Items.Count > 0)
			{
				cMenMCCopy.Enabled = true;
				cMenMCDelete.Enabled = true;
			}
			else
			{
				cMenMCCopy.Enabled = false;
				cMenMCDelete.Enabled = false;
			}
					
			if (lvErrorCollector.SelectedItems.Count > 0)
			{
				cMenMCCopy.Text = Language.strMenuCopy;
				cMenMCDelete.Text = Language.strMenuNotificationsDelete;
			}
			else
			{
				cMenMCCopy.Text = Language.strMenuNotificationsCopyAll;
				cMenMCDelete.Text = Language.strMenuNotificationsDeleteAll;
			}
		}
				
		private void cMenMCCopy_Click(object sender, EventArgs e)
		{
			CopyMessagesToClipboard();
		}
				
		private void CopyMessagesToClipboard()
		{
			try
			{
				IEnumerable items;
				if (lvErrorCollector.SelectedItems.Count > 0)
				{
					items = lvErrorCollector.SelectedItems;
				}
				else
				{
					items = lvErrorCollector.Items;
				}
						
				var stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("----------");
						
				lvErrorCollector.BeginUpdate();

			    foreach (ListViewItem item in items)
				{
					var message = item.Tag as Messages.Message;
					if (message == null)
					{
						continue;
					}
							
					stringBuilder.AppendLine(message.Class.ToString());
					stringBuilder.AppendLine(message.Date.ToString(CultureInfo.InvariantCulture));
					stringBuilder.AppendLine(message.Text);
					stringBuilder.AppendLine("----------");
				}
						
				Clipboard.SetText(stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "UI.Window.ErrorsAndInfos.CopyMessagesToClipboard() failed." + Environment.NewLine + ex.Message, true);
			}
			finally
			{
				lvErrorCollector.EndUpdate();
			}
		}
				
		private void cMenMCDelete_Click(object sender, EventArgs e)
		{
			DeleteMessages();
		}
				
		private void DeleteMessages()
		{
			try
			{
				lvErrorCollector.BeginUpdate();
						
				if (lvErrorCollector.SelectedItems.Count > 0)
				{
					foreach (ListViewItem item in lvErrorCollector.SelectedItems)
						item.Remove();
				}
				else
				{
					lvErrorCollector.Items.Clear();
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "UI.Window.ErrorsAndInfos.DeleteMessages() failed" + Environment.NewLine + ex.Message, true);
			}
			finally
			{
				lvErrorCollector.EndUpdate();
			}
		}
        #endregion

	    private enum ControlLayout
		{
			Vertical = 0,
			Horizontal = 1
		}
	}
}
