using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace mRemoteUG.Tests
{
    [TestFixture]
    public class BinaryFileTests
    {
        [Test]
        public void LargeAddressAwareFlagIsSet()
        {
            var exePath = GetTargetPath();
            Assert.That(IsLargeAware(exePath), Is.True);
        }

        /// <summary>
        /// The executable carries an icon of its own.
        /// </summary>
        /// <remarks>
        /// Asked of the file, not of the shell. <c>Icon.ExtractAssociatedIcon</c> cannot answer
        /// this: for an executable with no icon resource it hands back the shell's generic
        /// application icon instead of nothing, so a non-null result proves only that Windows has
        /// a fallback. <c>ExtractIconEx</c> with an index of -1 returns how many icons the file
        /// actually contains, which is the question.
        /// <para>
        /// This ran red before the icon existed - the count was 0 - and that is the whole point of
        /// it. Without an icon of its own the executable, its shortcuts and its Programs and
        /// Features entry all show the generic Windows mark while the running window shows
        /// something else entirely, which is what prompted the change.
        /// </para>
        /// </remarks>
        [Test]
        public void TheExecutableCarriesAnIconOfItsOwn()
        {
            var exePath = Path.GetFullPath(GetTargetPath());
            Assume.That(File.Exists(exePath), $"{exePath} has not been built.");

            Assert.That(IconCountIn(exePath), Is.GreaterThan(0),
                        "the executable has no icon resource, so Windows draws its generic " +
                        "application icon for it everywhere outside the running window");
        }

        /// <summary>
        /// The executable's embedded manifest asks Windows for Per-Monitor V2.
        /// </summary>
        /// <remarks>
        /// Asked of the built binary rather than of the source file, so it covers the embedding as
        /// well as the content: a manifest that was never attached is the same defect as one that
        /// stopped asking.
        /// <para>
        /// This exists because until now nothing but <c>--selftest</c> guarded it, and
        /// <c>--selftest</c> can only see the mode Windows actually granted. A process in a service
        /// session is not granted PerMonitorV2 at all, so on a build agent running as a Windows
        /// service that check has to report rather than insist - which would have left the
        /// declaration itself unguarded everywhere CI runs. This is the half that can be asserted
        /// anywhere.
        /// </para>
        /// <para>
        /// The manifest is parsed rather than searched for a substring. Its own comments mention
        /// PerMonitorV2 several times, so a <c>Contains</c> would pass on a manifest that had
        /// stopped asking for anything. Parsing also means malformed XML fails here rather than at
        /// process start - though only partly: the side-by-side parser is stricter than XML, so a
        /// manifest this accepts can still be rejected by Windows. See the note in
        /// Properties\app.manifest.
        /// </para>
        /// </remarks>
        [Test]
        public void TheEmbeddedManifestAsksForPerMonitorV2()
        {
            var exePath = Path.GetFullPath(GetTargetPath());
            Assume.That(File.Exists(exePath), $"{exePath} has not been built.");

            var manifest = EmbeddedManifestOf(exePath);
            Assert.That(manifest, Is.Not.Null.And.Not.Empty,
                        "the executable carries no application manifest, so Windows applies its " +
                        "defaults and the process is not DPI aware at all");

            XDocument document;
            try
            {
                document = XDocument.Parse(manifest);
            }
            catch (Exception ex)
            {
                Assert.Fail($"the embedded manifest is not well-formed XML ({ex.Message}). Windows " +
                            "rejects the whole manifest and the process dies before any managed " +
                            "code runs - check for angle brackets inside its comments.");
                return;
            }

            var awareness = document.Descendants()
                                   .Where(e => e.Name.LocalName == "dpiAwareness")
                                   .Select(e => e.Value.Trim())
                                   .ToArray();

            Assert.That(awareness, Is.EqualTo(new[] { "PerMonitorV2" }),
                        "Properties\\app.manifest no longer asks for exactly PerMonitorV2. The list " +
                        "is deliberately a single value - see the note in the manifest itself.");
        }

        private const int RtManifest = 24;
        private const int CreateProcessManifestResourceId = 1;
        private const uint LoadLibraryAsDataFile = 0x00000002;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr unused, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr resourceData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr module, IntPtr resource);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);

        /// <summary>
        /// The application manifest embedded in a PE file, or null if it carries none.
        /// </summary>
        /// <remarks>
        /// Loaded as a data file, which maps the image without running anything in it - the point
        /// being to read the manifest of the built executable, not to start it.
        /// </remarks>
        private static string EmbeddedManifestOf(string file)
        {
            var module = LoadLibraryEx(file, IntPtr.Zero, LoadLibraryAsDataFile);
            if (module == IntPtr.Zero)
                Assert.Fail($"could not open {file} to read its resources " +
                            $"(Win32 {Marshal.GetLastWin32Error()}).");

            try
            {
                var resource = FindResource(module, (IntPtr)CreateProcessManifestResourceId,
                                            (IntPtr)RtManifest);
                if (resource == IntPtr.Zero)
                    return null;

                var size = SizeofResource(module, resource);
                var data = LockResource(LoadResource(module, resource));
                if (size == 0 || data == IntPtr.Zero)
                    return null;

                var bytes = new byte[size];
                Marshal.Copy(data, bytes, 0, (int)size);
                return new UTF8Encoding(false).GetString(bytes);
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
                                                IntPtr[] phiconLarge, IntPtr[] phiconSmall,
                                                int nIcons);

        /// <summary>How many icons a file carries. -1 asks for the count rather than an icon.</summary>
        private static int IconCountIn(string file) => ExtractIconEx(file, -1, null, null, 0);

        public string GetTargetPath([CallerFilePath] string sourceFilePath = "")
        {
            const string debugOrRelease =
#if DEBUG
				"Debug";
#else
				"Release";
#endif

            var path = Path.GetDirectoryName(sourceFilePath);
            var filePath = $"{path}\\..\\mRemoteUG\\bin\\{debugOrRelease}\\mRemoteUG.exe";
            return filePath;
        }

        private bool IsLargeAware(string file)
        {
            using (var fs = File.OpenRead(file))
            {
                return IsLargeAware(fs);
            }
        }

        /// <summary>
        /// Checks if the stream is a MZ header and if it is large address aware
        /// </summary>
        /// <param name="stream">Stream to check, make sure its at the start of the MZ header</param>
        /// <returns></returns>
        private bool IsLargeAware(Stream stream)
        {
            const int imageFileLargeAddressAware = 0x20;

            var br = new BinaryReader(stream);

            if (br.ReadInt16() != 0x5A4D)       //No MZ Header
                return false;

            br.BaseStream.Position = 0x3C;
            var peHeaderLocation = br.ReadInt32();         //Get the PE header location.

            br.BaseStream.Position = peHeaderLocation;
            if (br.ReadInt32() != 0x4550)       //No PE header
                return false;

            br.BaseStream.Position += 0x12;
            return (br.ReadInt16() & imageFileLargeAddressAware) == imageFileLargeAddressAware;
        }
    }
}