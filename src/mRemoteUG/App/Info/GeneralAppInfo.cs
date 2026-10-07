using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;


namespace mRemoteUG.App.Info
{
	public static class GeneralAppInfo
	{
        /// <summary>
        /// The version as anything user-facing should show it: no build metadata.
        /// </summary>
        /// <remarks>
        /// <see cref="Application.ProductVersion"/> is the assembly's informational version,
        /// which the build composes as <c>&lt;file version&gt;+g&lt;commit&gt;</c> (ADR-0030). Measured on
        /// .NET 10: WinForms hands that string back whole, metadata included, so the About dialog
        /// would otherwise carry forty characters of commit hash.
        /// </remarks>
        public static string ApplicationVersion => TrimBuildMetadata(ApplicationVersionFull);

        /// <summary>
        /// The whole informational version, commit and all. What the log carries.
        /// </summary>
        /// <remarks>
        /// This machine cannot open a connection anywhere, so a fault is diagnosed by reading a
        /// log off the machine that saw it. Which commit produced the binary that wrote that log
        /// is not recoverable from anything else in it, which is why the hash is kept here even
        /// though it is trimmed everywhere a person looks.
        /// <para>
        /// Read off this assembly rather than through <see cref="Application.ProductVersion"/>,
        /// which answers for the <em>entry</em> assembly. Measured: under the NUnit test host that
        /// is testhost.exe, and the version came back as 17.11.1 - the test SDK's. The About dialog
        /// is supposed to name this application whatever is hosting it, and
        /// <see cref="Copyright"/> below already asks its own assembly for the same reason.
        /// </para>
        /// </remarks>
        public static readonly string ApplicationVersionFull =
            typeof(GeneralAppInfo).Assembly
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion
            ?? Application.ProductVersion;

        /// <summary>
        /// Everything before the first <c>+</c>: semantic versioning spells build metadata that
        /// way, and it is the part that identifies the build rather than describing it.
        /// </summary>
        internal static string TrimBuildMetadata(string version)
        {
            var metadata = version?.IndexOf('+') ?? -1;
            return metadata < 0 ? version : version.Substring(0, metadata);
        }

        public static readonly string ProductName = Application.ProductName;
        public static readonly string Copyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute), false)).Copyright;
        public static readonly string HomePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
	}
}