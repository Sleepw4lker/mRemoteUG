using mRemoteUG.Tools.Cmdline;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools.Cmdline;

[TestFixture]
public class CommandLineArgumentsTests
{
    [TestCase("plain", "plain")]
    [TestCase("with space", "\"with space\"")]
    [TestCase("", "\"\"")]
    [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [TestCase("say\"hi", "say\\\"hi")]
    [TestCase("back\\slash", "back\\slash")]
    [TestCase("C:\\a b\\", "\"C:\\a b\\\\\"")]
    [TestCase("C:\\dir\\", "C:\\dir\\")]
    [TestCase("a b\\\\", "\"a b\\\\\\\\\"")]
    [TestCase("\\\"", "\\\\\\\"")]
    [TestCase("a \\\"b", "\"a \\\\\\\"b\"")]
    public void AnArgumentIsQuotedAndEscapedAsCommandLineToArgvWExpects(string argument, string expected)
    {
        var arguments = new CommandLineArguments();
        arguments.Add(argument);

        Assert.That(arguments.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ForcingQuotesQuotesAnArgumentWithoutSpaces()
    {
        var arguments = new CommandLineArguments();
        arguments.Add("plain", true);

        Assert.That(arguments.ToString(), Is.EqualTo("\"plain\""));
    }

    [Test]
    public void NotForcingQuotesLeavesAnArgumentWithoutSpacesAlone()
    {
        var arguments = new CommandLineArguments();
        arguments.Add("plain", false);

        Assert.That(arguments.ToString(), Is.EqualTo("plain"));
    }

    [Test]
    public void ForcedQuotesDoubleTheTrailingBackslashes()
    {
        var arguments = new CommandLineArguments();
        arguments.Add("C:\\dir\\", true);

        Assert.That(arguments.ToString(), Is.EqualTo("\"C:\\dir\\\\\""));
    }

    [Test]
    public void ArgumentsAreJoinedBySingleSpaces()
    {
        var arguments = new CommandLineArguments();
        arguments.Add("one", "two words", "three");

        Assert.That(arguments.ToString(), Is.EqualTo("one \"two words\" three"));
    }

    [Test]
    public void ArgumentsAddedSeparatelyKeepTheirOrder()
    {
        var arguments = new CommandLineArguments();
        arguments.Add("one");
        arguments.Add("two", true);
        arguments.Add("three");

        Assert.That(arguments.ToString(), Is.EqualTo("one \"two\" three"));
    }

    [Test]
    public void NoArgumentsGiveAnEmptyString()
    {
        Assert.That(new CommandLineArguments().ToString(), Is.EqualTo(""));
    }

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("plain", "plain")]
    [TestCase("\"", "\\\"")]
    [TestCase("\\\"", "\\\\\\\"")]
    [TestCase("\\\\\"", "\\\\\\\\\\\"")]
    [TestCase("\\\\x", "\\\\x")]
    [TestCase("a\"b\"c", "a\\\"b\\\"c")]
    public void BackslashesBeforeAQuoteAreDoubledAndTheQuoteIsEscaped(string argument, string expected)
    {
        Assert.That(CommandLineArguments.EscapeBackslashes(argument), Is.EqualTo(expected));
    }

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("plain", "plain")]
    [TestCase("a\\", "a\\\\")]
    [TestCase("a\\\\", "a\\\\\\\\")]
    [TestCase("a\\b", "a\\b")]
    [TestCase("a\\b\\", "a\\b\\\\")]
    public void TrailingBackslashesAreDoubled(string argument, string expected)
    {
        Assert.That(CommandLineArguments.EscapeBackslashesForTrailingQuote(argument), Is.EqualTo(expected));
    }

    [TestCase("plain", false, "plain")]
    [TestCase("plain", true, "\"plain\"")]
    [TestCase("a b", false, "\"a b\"")]
    [TestCase("", false, "\"\"")]
    [TestCase(null, false, "\"\"")]
    [TestCase("a b\\", false, "\"a b\\\\\"")]
    public void QuotingOnlyHappensWhenItIsNeeded(string argument, bool forceQuotes, string expected)
    {
        Assert.That(CommandLineArguments.QuoteArgument(argument, forceQuotes), Is.EqualTo(expected));
    }

    [Test]
    public void QuoteArgumentDoesNotForceQuotesByDefault()
    {
        Assert.That(CommandLineArguments.QuoteArgument("plain"), Is.EqualTo("plain"));
    }
}
