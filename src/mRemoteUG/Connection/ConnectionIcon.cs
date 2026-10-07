using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using mRemoteUG.App;
using mRemoteUG.UI;


namespace mRemoteUG.Connection
{
	/// <summary>
	/// The marks a user can pick for a saved connection.
	/// </summary>
	/// <remarks>
	/// Stored as PNGs at every ladder size rather than as .ico files, which is the opposite of
	/// what a window icon does and for the opposite reason. These are tinted to the theme and are
	/// drawn by this application at a size it chooses, so nothing here wants Windows' own frame
	/// selection; and the BCL has no ICO writer, so the generator would have to emit the container
	/// by hand for no gain. Window icons stay .ico, where Windows does the picking.
	/// </remarks>
	public class ConnectionIcon : StringConverter
	{
		private const string ResourcePrefix = "mRemoteUG.Icons.";
		private const string ResourceSuffix = ".png";

		/// <summary>
		/// The names of every connection icon embedded in this assembly, alphabetically.
		/// </summary>
		/// <remarks>
		/// Built from the assembly manifest on first use, so the converter works before startup
		/// has run rather than depending on an initialisation order. Each icon is embedded once
		/// per ladder size as <c>Name.size.png</c>, so the size is trimmed back off and the
		/// results de-duplicated.
		/// </remarks>
		private static readonly Lazy<string[]> IconNames = new Lazy<string[]>(() =>
			typeof(ConnectionIcon).Assembly.GetManifestResourceNames()
				.Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
				            n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
				.Select(n => n.Substring(ResourcePrefix.Length,
				                         n.Length - ResourcePrefix.Length - ResourceSuffix.Length))
				.Select(TrimSize)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
				.ToArray());

		/// <summary>Drops the ".16" from "Remote Desktop.16"; names themselves carry no dot.</summary>
		private static string TrimSize(string stem)
		{
			var dot = stem.LastIndexOf('.');
			return dot < 0 ? stem : stem.Substring(0, dot);
		}

		public static string[] Icons => IconNames.Value;

		/// <summary>
		/// The current name for one that may have been written by an older build.
		/// </summary>
		/// <remarks>
		/// The icon set was curated when it moved to Fluent: some names went (a fax machine, a
		/// telephone, a SharePoint server) and some arrived. A connections file written before
		/// that still names the old ones, so they are healed to their nearest replacement on read.
		/// <para>
		/// <strong>On read only.</strong> Nothing rewrites the file behind the user, which mirrors
		/// what <see cref="DefaultIconName"/> already does for the pre-rename product name.
		/// </para>
		/// <para>
		/// The table is explicit rather than a fallback, and that is load-bearing: an unknown name
		/// must still resolve to nothing. <c>--selftest</c> asserts exactly that, because an icon
		/// picker that answers every string would hide a dropped csproj glob completely.
		/// </para>
		/// </remarks>
		public static string Heal(string iconName)
		{
			if (string.IsNullOrEmpty(iconName))
				return null;

			if (Icons.Contains(iconName, StringComparer.Ordinal))
				return iconName;

			return Resources.LegacyConnectionIcons.TryGetValue(iconName, out var healed) ? healed : null;
		}

		/// <summary>Every name an older build may have written, and what each heals to.</summary>
		public static IReadOnlyDictionary<string, string> LegacyNames => Resources.LegacyConnectionIcons;

		/// <summary>
		/// The icon name a new connection should be given.
		/// </summary>
		/// <remarks>
		/// Resolved rather than taken at face value. <c>ConDefaultIcon</c> is user-scoped and
		/// startup calls <c>Settings.Upgrade</c>, which carries the previous version's value
		/// forward out of its own user.config - so every profile that ever ran a build from before
		/// the product was renamed still asks for an icon named after the old product. That name is
		/// no longer embedded, so every connection created on such a profile was given an icon that
		/// resolves to nothing, and <c>--selftest</c> failed on it on any machine with a history.
		/// <para>
		/// Falling back to the value the application ships with lets those profiles heal themselves
		/// rather than needing the setting cleared by hand. The shipped value is read from the
		/// settings property rather than written out again here, so there is one place that says
		/// what the default is.
		/// </para>
		/// </remarks>
		public static string DefaultIconName
		{
			get
			{
				var configured = Heal(Settings.Default.ConDefaultIcon);
				if (!string.IsNullOrEmpty(configured))
					return configured;

				var shipped = Settings.Default.Properties[nameof(Settings.Default.ConDefaultIcon)]
				                      ?.DefaultValue as string ?? "";
				return Heal(shipped) ?? "";
			}
		}

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			return new StandardValuesCollection(Icons);
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		/// <summary>
		/// Loads a connection icon at the ladder size covering the one asked for.
		/// </summary>
		/// <remarks>
		/// The size argument does something now. It used to be kept "because it is the only
		/// correct way to ask" against artwork that had a single 16x16 frame and ignored it.
		/// <para>
		/// The returned bitmap is <strong>shared and must not be disposed</strong> - it comes out
		/// of the same cache the glyphs use, which is also what lets a DPI change re-pick it.
		/// </para>
		/// </remarks>
		/// <returns>The icon, or null when no icon of that name is embedded or healable.</returns>
		public static Bitmap FromString(string iconName, int size = 16)
		{
			// A name read from a connections file never reaches the file system, so a value
			// carrying path separators simply fails to heal rather than escaping the set.
			var healed = Heal(iconName);
			if (healed == null)
				return null;

			try
			{
				return Glyphs.FromStem(ResourcePrefix + healed, tint: true, devicePixels: size);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
				    $"Couldn\'t get Icon from String" + Environment.NewLine + ex.Message);
			}

			return null;
		}

		/// <summary>
		/// A connection icon as an <see cref="Icon"/>, for the one caller that needs one.
		/// </summary>
		/// <remarks>
		/// A session tab's <c>Icon</c> property will not take a bitmap. <c>Icon.FromHandle</c>
		/// does <em>not</em> own the handle it is given and will not free it, so one of these per
		/// tab would leak an HICON per session opened. They are cached instead and never released,
		/// which is bounded by the size of the icon set times the ladder.
		/// </remarks>
		public static Icon TabIconFor(string iconName, int size = 16)
		{
			var bitmap = FromString(iconName, size);
			if (bitmap == null)
				return null;

			lock (TabIcons)
			{
				var key = (Heal(iconName), bitmap.Width);
				if (TabIcons.TryGetValue(key, out var cached))
					return cached;

				var icon = Icon.FromHandle(bitmap.GetHicon());
				TabIcons[key] = icon;
				return icon;
			}
		}

		private static readonly Dictionary<(string Name, int Size), Icon> TabIcons =
			new Dictionary<(string, int), Icon>();
	}
}
