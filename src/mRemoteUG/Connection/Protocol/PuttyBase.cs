using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tools.Cmdline;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Connection.Protocol
{
	public class PuttyBase : ProtocolBase
	{
		private const int IDM_RECONF = 0x50; // PuTTY Settings Menu ID
	    private bool _isPuttyNg;

	    #region Public Properties

	    protected Putty_Protocol PuttyProtocol { private get; set; }

        protected Putty_SSHVersion PuttySSHVersion { private get; set; }

	    public IntPtr PuttyHandle { get; set; }

	    private Process PuttyProcess { get; set; }

	    public static string PuttyPath { get; set; }

	    public bool Focused
	    {
	        get { return NativeMethods.GetForegroundWindow() == PuttyHandle; }
	    }

	    #endregion

	    #region Private Events & Handlers
		private void ProcessExited(object? sender, EventArgs e)
		{
            // Raising Closed directly used to close the tab while leaving everything else behind:
            // the hosted control and InterfaceControl were never disposed and the reconnect timer
            // kept running. Go through Close() so a session that ends on its own is torn down the
            // same way as one the user closes. Close() is idempotent and marshals to the UI thread,
            // which matters here because this runs on a Process.Exited thread-pool callback.
            Close();
		}
        #endregion

        #region Public Methods
		public override bool Connect()
		{
			try
			{
				var unusableReason = PuttyPathProvider.GetUnusableReason();
				if (unusableReason != null)
				{
					Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
						Language.strPuttyConnectionFailed + Environment.NewLine + unusableReason +
						Environment.NewLine + Language.strPuttyConfigureHint);
					return false;
				}

				_isPuttyNg = PuttyTypeDetector.GetPuttyType() == PuttyTypeDetector.PuttyType.PuttyNg;

			    PuttyProcess = new Process
			    {
			        StartInfo =
			        {
			            UseShellExecute = false,
			            FileName = PuttyPath
			        }
			    };

			    var arguments = new CommandLineArguments();

			    arguments.Add("-load", InterfaceControl.Info.PuttySession);

				if (!(InterfaceControl.Info is PuttySessionInfo))
				{
					arguments.Add("-" + PuttyProtocol);

					if (PuttyProtocol == Putty_Protocol.ssh)
					{
						var username = "";
						var password = "";

						if (!string.IsNullOrEmpty(InterfaceControl.Info?.Username))
						{
							username = InterfaceControl.Info.Username;
						}
						else
						{
						    // ReSharper disable once SwitchStatementMissingSomeCases
						    switch (Settings.Default.EmptyCredentials)
						    {
						        case "windows":
						            username = Environment.UserName;
						            break;
						        case "custom":
						            username = Settings.Default.DefaultUsername;
						            break;
						    }
						}

						if (!string.IsNullOrEmpty(InterfaceControl.Info?.Password))
						{
							password = InterfaceControl.Info.Password;
						}
						else
						{
							if (Settings.Default.EmptyCredentials == "custom")
							{
                                password = DefaultCredentials.Password;
							}
						}

						arguments.Add("-" + (int)PuttySSHVersion);

						if (((int)Force & (int)ConnectionInfo.Force.NoCredentials) != (int)ConnectionInfo.Force.NoCredentials)
						{
							if (!string.IsNullOrEmpty(username))
							{
								arguments.Add("-l", username);
							}
							if (!string.IsNullOrEmpty(password))
							{
								arguments.Add("-pw", password);
							}
						}
					}

					arguments.Add("-P", InterfaceControl.Info.Port.ToString());
					arguments.Add(InterfaceControl.Info.Hostname);
				}

				if (_isPuttyNg)
				{
					arguments.Add("-hwndparent", InterfaceControl.Handle.ToString());
				}

				PuttyProcess.StartInfo.Arguments = arguments.ToString();

				PuttyProcess.EnableRaisingEvents = true;
				PuttyProcess.Exited += ProcessExited;

				PuttyProcess.Start();
				PuttyProcess.WaitForInputIdle(Settings.Default.MaxPuttyWaitTime * 1000);

				// The probe differs by build: PuTTYNG creates its window as a child of the panel
				// named in -hwndparent, stock PuTTY as a top-level window of its own.
				Func<IntPtr> probe;
				if (_isPuttyNg)
				{
					probe = () => NativeMethods.FindWindowEx(InterfaceControl.Handle, IntPtr.Zero, null, null);
				}
				else
				{
					probe = () =>
					{
						PuttyProcess.Refresh();
						return PuttyProcess.MainWindowHandle;
					};
				}

				PuttyHandle = HostedPuttyWindow.WaitForHandle(
					probe, TimeSpan.FromSeconds(Settings.Default.MaxPuttyWaitTime));

				if (PuttyHandle == IntPtr.Zero)
				{
					// This used to fall through: SetParent(0, ...) failed silently and the tab
					// opened empty while PuTTY ran on its own somewhere.
					throw new TimeoutException(
						$"PuTTY showed no window within {Settings.Default.MaxPuttyWaitTime}s " +
						"(Options > Advanced > PuTTY wait time).");
				}

				if (!_isPuttyNg)
				{
					// Stock PuTTY arrives as a top-level window with a caption and a frame, and it
					// puts them back on every SIZE_RESTORED - see HostedPuttyWindow.Adopt for why
					// the answer is to maximise it rather than to strip them.
					HostedPuttyWindow.Adopt(PuttyHandle, InterfaceControl.Handle);
				}

				// One line rather than four: these are four parts of the same fact, and they cost
				// four Information entries per PuTTY session in every log.
				Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
					string.Join(", ",
						Language.strPuttyStuff,
						string.Format(Language.strPuttyHandle, PuttyHandle),
						string.Format(Language.strPuttyTitle, PuttyProcess.MainWindowTitle),
						string.Format(Language.strPuttyParentHandle, InterfaceControl.Parent.Handle)), true);

				Resize(this, new EventArgs());
				LogHostedWindow();
				base.Connect();
				return true;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyConnectionFailed + Environment.NewLine + ex.Message);
				return false;
			}
		}

		public override void Focus()
		{
			try
			{
				if (ConnectionWindow.InTabDrag)
				{
					return;
				}
				HostedPuttyWindow.Focus(PuttyHandle);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyFocusFailed + Environment.NewLine + ex.Message, true);
			}
		}

		/// <summary>
		/// Records how Windows is hosting the PuTTY window: its DPI awareness, whether it is
		/// still the chromeless, maximised child it was made, and whether the window Windows
		/// actually produced is the size that was asked for.
		/// </summary>
		/// <remarks>
		/// PuTTY's own manifest decides the awareness and we do not control it. An unaware or
		/// system-aware build hosted inside a per-monitor-aware window is bitmap-scaled by Windows,
		/// which is correct in size and soft in appearance; that is inherent to mixed-mode hosting
		/// and is not a fault to be fixed by making this process unaware again.
		/// <para>
		/// The chrome readback runs after the first Resize, which is the first chance PuTTY has
		/// had to put its title bar back. A caption at that point is a Warning: it is the defect
		/// 890b245 shipped, and this line is the only way to see it from a log file on another
		/// machine.
		/// </para>
		/// <para>
		/// The actual-rect readback exists for one unreproduced report: after undocking a laptop
		/// from a 200% external monitor onto its own 150% panel, a newly opened stock PuTTY session
		/// filled only about two thirds of the panel, while sessions already open at the time of
		/// the undock stayed correctly sized. <see cref="NativeMethods.GetWindowRect"/>, called
		/// from this (per-monitor-aware) thread, is not DPI-virtualized, so comparing it against
		/// the panel size told <see cref="HostedPuttyWindow.Adopt"/>'s <c>MoveWindow</c> distinguishes
		/// the two possible faults: Windows not honouring the requested size for a lower-than-PMv2
		/// -aware child (actual rect smaller than the panel), versus the panel itself already being
		/// wrong when this ran (actual rect matches the panel, both wrong). Nothing here fixes
		/// either - there is no multi-monitor, mixed-DPI hardware on this machine to reproduce it
		/// on - this is only the readback that will say which one it was, the next time it happens.
		/// </para>
		/// </remarks>
		private void LogHostedWindow()
		{
			try
			{
				if (PuttyHandle == IntPtr.Zero)
					return;

				var awareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(
					NativeMethods.GetWindowDpiAwarenessContext(PuttyHandle));
				var state = HostedPuttyWindow.Describe(PuttyHandle);

				string actualRectDescription;
				if (NativeMethods.GetWindowRect(PuttyHandle, out var actualRect))
				{
					var actualWidth = actualRect.right - actualRect.left;
					var actualHeight = actualRect.bottom - actualRect.top;
					var screen = Screen.FromHandle(PuttyHandle);
					actualRectDescription =
						$"actual {actualWidth}x{actualHeight} on screen {screen.DeviceName} " +
						$"(bounds {screen.Bounds.Width}x{screen.Bounds.Height}, " +
						$"work area {screen.WorkingArea.Width}x{screen.WorkingArea.Height})";

					if (actualWidth != InterfaceControl.Width || actualHeight != InterfaceControl.Height)
					{
						Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
							"PuTTY window size does not match the panel it was asked to fill: " +
							$"asked for {InterfaceControl.Width}x{InterfaceControl.Height}, got " +
							actualRectDescription + ".", true);
					}
				}
				else
				{
					actualRectDescription = "actual rect unavailable (GetWindowRect failed)";
				}

				Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
					$"PuTTY window hosting: child awareness {awareness}, child dpi " +
					$"{NativeMethods.GetDpiForWindow(PuttyHandle)}, host dpi " +
					$"{NativeMethods.GetDpiForWindow(InterfaceControl.Handle)}, " +
					$"panel {InterfaceControl.Width}x{InterfaceControl.Height}, {actualRectDescription}, " +
					$"{state}.", true);

				if (!_isPuttyNg && state.HasCaption)
				{
					Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
						"PuTTY reinstated its title bar; the hosted window is not maximised. " + state, true);
				}
			}
			catch (Exception ex)
			{
				// A failure, not a fact: reporting it as Information is why nobody ever noticed it
				// happening.
				Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
					"Could not read back the PuTTY window's hosting state" + Environment.NewLine + ex.Message, true);
			}
		}

		/// <summary>
		/// A DPI change resizes nothing by itself, so the hosted window has to be repositioned.
		/// </summary>
		public override void NotifyDpiChanged(int dpi)
		{
			base.NotifyDpiChanged(dpi);
			Resize(this, EventArgs.Empty);
		}

		public override void Resize(object? sender, EventArgs e)
		{
			try
			{
				if (InterfaceControl.Size == Size.Empty)
				{
					return;
				}

                // Both branches are the same. Stock PuTTY used to be positioned at negative
                // offsets so its caption and frame fell outside the panel, with the offsets
                // computed from SystemInformation.CaptionHeight, FrameBorderSize and the resize
                // border thicknesses. Every one of those wraps GetSystemMetrics, which under a
                // per-monitor-aware thread answers for the *system* DPI - so on any monitor that is
                // not at the system scale the chrome was mispositioned by the difference.
                //
                // Now there is no chrome to hide: PuTTYNG never had any, and stock PuTTY is hosted
                // maximised (HostedPuttyWindow.Adopt), which is the one state in which it leaves
                // its caption off. Moving a maximised window is allowed and does not restore it.
                NativeMethods.MoveWindow(PuttyHandle, 0, 0, InterfaceControl.Width, InterfaceControl.Height, true);
            }
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyResizeFailed + Environment.NewLine + ex.Message, true);
			}
		}

		/// <summary>
		/// Kills and releases the PuTTY process.
		/// </summary>
		/// <remarks>
		/// This used to be a Close() override that ran before base.Close(). Hanging it on the
		/// cleanup hook instead means it is covered by the base class's run-once guard and happens
		/// at the right point in the teardown - after handlers are detached, before the hosted
		/// control is disposed. Killing the process raises Exited, which calls Close() again; the
		/// guard absorbs that.
		/// </remarks>
		protected override void CleanupProtocolResources()
		{
			try
			{
				if (PuttyProcess?.HasExited == false)
				{
					PuttyProcess.Kill();
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyKillFailed + Environment.NewLine + ex.Message, true);
			}

			try
			{
				if (PuttyProcess != null)
				{
					PuttyProcess.Exited -= ProcessExited;
					PuttyProcess.Dispose();
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyDisposeFailed + Environment.NewLine + ex.Message, true);
			}

			PuttyProcess = null;
		}

		public void ShowSettingsDialog()
		{
			try
			{
                NativeMethods.PostMessage(PuttyHandle, NativeMethods.WM_SYSCOMMAND, (IntPtr)IDM_RECONF, (IntPtr)0);
                NativeMethods.SetForegroundWindow(PuttyHandle);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strPuttyShowSettingsDialogFailed + Environment.NewLine + ex.Message, true);
			}
		}
        #endregion

        #region Enums

	    protected enum Putty_Protocol
		{
			ssh = 0,
			telnet = 1,
			rlogin = 2,
			raw = 3,
			serial = 4
		}

	    protected enum Putty_SSHVersion
		{
			ssh1 = 1,
			ssh2 = 2
		}
        #endregion
	}
}