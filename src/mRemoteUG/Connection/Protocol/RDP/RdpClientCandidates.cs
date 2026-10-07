using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AxMSTSCLib;

namespace mRemoteUG.Connection.Protocol.RDP
{
    /// <summary>
    /// The RDP ActiveX coclasses this application will drive, newest first, and the one place
    /// that walks them.
    /// </summary>
    /// <remarks>
    /// The floor is v11, whose default interface is <c>IMsRdpClient10</c> - the newest client
    /// interface the mstscax type library declares at all. v12 shares that default interface
    /// (the same IID, {7ED92C39-EB38-4927-A70A-708AC5A59321}), so one managed type drives
    /// either coclass, and a machine on which v12 is not registered is a supported machine
    /// rather than a fault. Windows 11 always registers v11.
    /// <para>
    /// The chain used to exist twice: here and hand-copied into <c>SelfTest.CheckRdpControl</c>,
    /// whose comment claimed it was "the same chain RdpProtocol uses" with nothing enforcing it.
    /// Sharing only the list would still let the two walks diverge in how they parent, create
    /// and dispose candidates - which is exactly where CoCreateInstance happens - so the walk
    /// lives here too.
    /// </para>
    /// </remarks>
    internal static class RdpClientCandidates
    {
        internal static readonly Func<AxHost>[] NewestFirst =
        {
            () => new AxMsRdpClient12NotSafeForScripting(),
            () => new AxMsRdpClient11NotSafeForScripting(),
        };

        /// <summary>
        /// Creates the newest control this machine will give us, parented into
        /// <paramref name="parent"/> and ready to use.
        /// </summary>
        /// <param name="attempts">
        /// Receives one line per candidate, success or failure. Always populated, so a machine
        /// that merely lacks v12 can be told apart from one that has no RDP control at all.
        /// </param>
        /// <returns>The created control, or null when every candidate refused.</returns>
        internal static AxHost CreateNewest(Control parent, string name, DockStyle dock, IList<string> attempts)
        {
            foreach (var factory in NewestFirst)
            {
                var candidate = factory();

                // Everything here - including the Parent assignment - must stay inside the try:
                // parenting an AxHost into an already-visible container can synchronously cascade
                // into CreateHandle()/CoCreateInstance() (via OnVisibleChanged/WmShowWindow) before
                // CreateControl() below is ever reached, so that's where an unavailable coclass
                // (CLASS_E_CLASSNOTAVAILABLE) actually throws.
                try
                {
                    candidate.Name = name;
                    candidate.Dock = dock;
                    candidate.Parent = parent;
                    candidate.CreateControl();
                    attempts.Add($"{candidate.GetType().Name}: created");
                    return candidate;
                }
                catch (COMException ex)
                {
                    attempts.Add($"{candidate.GetType().Name}: unavailable (0x{ex.HResult:X8})");
                    try
                    {
                        candidate.Parent = null;
                        candidate.Dispose();
                    }
                    catch (Exception)
                    {
                        /* already gone */
                    }
                }
            }

            return null;
        }
    }
}
