#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages
{
    /// <summary>
    /// Keeps the rule that everything logged goes through the Options -> Notifications filter.
    /// </summary>
    /// <remarks>
    /// The filter is applied by a decorator in front of the text log writer, so any code that
    /// reaches for <c>Logger.Instance.Log</c> itself silently opts out of the user's settings -
    /// which is exactly what the teardown watchdog used to do. There is no way to notice that from
    /// the outside; the log just contains something the user asked not to have. Hence a scan.
    /// </remarks>
    [TestFixture]
    public class LoggingRoutingTests
    {
        /// <summary>
        /// The only two places allowed to talk to the log sink directly, each for a stated reason.
        /// </summary>
        private static readonly (string File, string Reason)[] SanctionedDirectCallers =
        {
            ("TextLogMessageWriter.cs", "the sanctioned bridge - the filter decorator sits in front of it"),
            ("SelfTest.cs", "probes the log file itself, so it must not be subject to the filter")
        };

        [Test]
        public void DirectLogSinkCallsAreConfinedToTheSanctionedSites()
        {
            var allowed = SanctionedDirectCallers.Select(c => c.File).ToArray();

            var offenders = SourceFiles()
                            .Where(CallsTheLoggerDirectly)
                            .Select(Path.GetFileName)
                            .Where(name => !allowed.Contains(name))
                            .OrderBy(name => name)
                            .ToArray();

            Assert.That(offenders, Is.Empty,
                        "These write to the log sink directly and so ignore Options -> Notifications -> Logging: " +
                        string.Join(", ", offenders) + ". Write through FilteredLogWriter instead, which applies " +
                        "the same filter as the message collector and is safe to call from any thread. Only " +
                        string.Join(" and ", SanctionedDirectCallers.Select(c => $"{c.File} ({c.Reason})")) +
                        " are exempt.");
        }

        [Test]
        public void TheLogWriterIsReachedThroughAFilterEverywhereItIsBuilt()
        {
            var constructions = SourceFiles()
                                .Where(f => File.ReadAllText(f).Contains("new TextLogMessageWriter("))
                                .Select(Path.GetFileName)
                                .OrderBy(name => name)
                                .ToArray();

            // FilteredLogWriter owns the one the application uses; SelfTest builds its own to prove
            // the filter works, which is the point of that check rather than a bypass of it.
            Assert.That(constructions, Is.EquivalentTo(new[] { "FilteredLogWriter.cs", "SelfTest.cs" }),
                        "A text log writer is being built somewhere new. It must have a " +
                        "MessageTypeFilterDecorator in front of it, or the Logging settings will not apply.");
        }

        /// <summary>
        /// Whether the file reaches for the log sink itself, either to call a level on it or
        /// to take a reference it can call later.
        /// </summary>
        /// <remarks>
        /// Comment lines are skipped so that explaining the rule does not break it, and the
        /// trailing "." or ";" is what separates a real call from <c>Logger.Instance.LogPath</c>,
        /// which is just the path and carries no message.
        /// </remarks>
        private static bool CallsTheLoggerDirectly(string file)
        {
            return File.ReadLines(file)
                       .Select(line => line.TrimStart())
                       .Where(line => !line.StartsWith("//") && !line.StartsWith("*"))
                       .Any(line => Regex.IsMatch(line, @"Logger\.Instance\.Log\s*[.;]"));
        }

        private static string[] SourceFiles([CallerFilePath] string sourceFilePath = "")
        {
            var testsDirectory = Path.GetDirectoryName(sourceFilePath)
                                 ?? throw new InvalidOperationException("Could not locate the test source.");
            var productDirectory = Path.GetFullPath(Path.Combine(testsDirectory, "..", "..", "mRemoteUG"));

            Assert.That(Directory.Exists(productDirectory), Is.True,
                        $"Could not find the product source at {productDirectory}.");

            return Directory.GetFiles(productDirectory, "*.cs", SearchOption.AllDirectories)
                            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                            .ToArray();
        }
    }
}
