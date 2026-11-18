using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tools.Cmdline;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools.Cmdline;

[TestFixture]
public class CmdArgumentsInterpreterTests
{
    private static readonly string[] s_cCXYXmlArguments = { "/c", @"C:\x\y.xml" };
    private static readonly string[] s_keyHttpsHostABCArguments = { "-key", "https://host/a?b=c" };
    private static readonly string[] s_keyABCArguments = { "/key=a:b=c" };
    private static readonly string[] s_flagArguments = { "/flag" };
    private static readonly string[] s_firstSecondValueArguments2 = { "/first", "/second", "value" };
    private static readonly string[] s_firstSecondValueArguments = { "/first", "/second=value" };
    private static readonly string[] s_keyValueTrailingArguments = { "/key", "value", "/trailing" };
    private static readonly string[] s_strayKeyValueArguments = { "stray", "/key", "value", "stray2" };
    private static readonly string[] s_keyOneTwoArguments = { "/key", "one", "two" };
    private static readonly string[] s_keyFirstKeyOtherArguments = { "/key=first", "/key", "/other" };
    private static readonly string[] s_keyFirstKeyArguments = { "/key=first", "/key" };
    private static readonly string[] s_keyFirstKeyOtherXArguments = { "/key=first", "/key", "/other=x" };
    private static readonly string[] s_resetposArguments = { "/ResetPos" };
    private static readonly string[] s_keyArguments = { "/key" };
    private static readonly string[] s_keyValueStrayArguments = { "/key", "value", "stray", "/flag=x" };

    [TestCase("/key", "value", "value")]
    [TestCase("-key", "value", "value")]
    [TestCase("--key", "value", "value")]
    [TestCase("--key", "\"a b\\c.xml\"", "a b\\c.xml")]
    [TestCase("-key", "'quoted'", "quoted")]
    [TestCase("/key", "\"half", "half")]
    public void ASeparateValueBelongsToTheKeyBeforeIt(string key, string value, string expected)
    {
        var args = new CmdArgumentsInterpreter(new[] { key, value });

        Assert.That(args["key"], Is.EqualTo(expected));
    }

    [TestCase("-key=value", "value")]
    [TestCase("--key:value", "value")]
    [TestCase("/key=value", "value")]
    [TestCase("/key:\"C:\\a b\\c.xml\"", "C:\\a b\\c.xml")]
    [TestCase("/key='quoted'", "quoted")]
    [TestCase("/key=a=b", "a=b")]
    [TestCase("/key:a:b", "a:b")]
    [TestCase("/key=", "")]
    public void AnEnclosedValueIsSplitOffTheKey(string argument, string expected)
    {
        var args = new CmdArgumentsInterpreter(new[] { argument });

        Assert.That(args["key"], Is.EqualTo(expected));
    }

    [Test]
    public void ADriveLetterPathAfterASpaceIsTheValueOfTheSwitch()
    {
        var args = new CmdArgumentsInterpreter(s_cCXYXmlArguments);

        Assert.That(args["c"], Is.EqualTo(@"C:\x\y.xml"));
        Assert.That(args[@"\x\y.xml"], Is.Null);
    }

    [Test]
    public void AValueIsNeverSplitEvenWithAColonAndAnEqualsSign()
    {
        var args = new CmdArgumentsInterpreter(s_keyHttpsHostABCArguments);

        Assert.That(args["key"], Is.EqualTo("https://host/a?b=c"));
    }

    [Test]
    public void AnEnclosedValueIsSplitAtTheFirstSeparator()
    {
        var args = new CmdArgumentsInterpreter(s_keyABCArguments);

        Assert.That(args["key"], Is.EqualTo("a:b=c"));
    }

    [Test, Combinatorial]
    public void ASwitchTakesAPathValueInEverySpelling(
        [Values("-", "--", "/")] string prefix,
        [Values(" ", ":", "=")] string separator,
        [Values(@"C:\x\y.xml", "\"C:\\x y\\z.xml\"", @"\\server\share\f.xml", "work.xml")] string value)
    {
        string[] arguments = separator == " "
            ? new[] { prefix + "cons", value }
            : new[] { prefix + "cons" + separator + value };

        var args = new CmdArgumentsInterpreter(arguments);

        Assert.That(args["cons"], Is.EqualTo(value.Trim('"')));
    }

    [TestCase("-")]
    [TestCase("--")]
    [TestCase("/")]
    public void AFlagWithoutAValueIsTrue(string prefix)
    {
        var args = new CmdArgumentsInterpreter(new[] { prefix + "reset" });

        Assert.That(args["reset"], Is.EqualTo("true"));
    }

    [TestCase("-")]
    [TestCase("--")]
    [TestCase("/")]
    public void AFlagFollowedByASwitchWithAPathIsTrue(string prefix)
    {
        var args = new CmdArgumentsInterpreter(new[] { prefix + "reset", prefix + "c", @"C:\x\y.xml" });

        Assert.That(args["reset"], Is.EqualTo("true"));
        Assert.That(args["c"], Is.EqualTo(@"C:\x\y.xml"));
    }

    [Test]
    public void AFlagAloneIsTrue()
    {
        var args = new CmdArgumentsInterpreter(s_flagArguments);

        Assert.That(args["flag"], Is.EqualTo("true"));
    }

    [Test]
    public void AFlagFollowedByAnotherFlagIsTrue()
    {
        var args = new CmdArgumentsInterpreter(s_firstSecondValueArguments2);

        Assert.That(args["first"], Is.EqualTo("true"));
        Assert.That(args["second"], Is.EqualTo("value"));
    }

    [Test]
    public void AFlagFollowedByAnEnclosedValueIsTrue()
    {
        var args = new CmdArgumentsInterpreter(s_firstSecondValueArguments);

        Assert.That(args["first"], Is.EqualTo("true"));
        Assert.That(args["second"], Is.EqualTo("value"));
    }

    [Test]
    public void ATrailingFlagIsTrue()
    {
        var args = new CmdArgumentsInterpreter(s_keyValueTrailingArguments);

        Assert.That(args["key"], Is.EqualTo("value"));
        Assert.That(args["trailing"], Is.EqualTo("true"));
    }

    [Test]
    public void AValueWithoutAKeyIsSkipped()
    {
        var args = new CmdArgumentsInterpreter(s_strayKeyValueArguments);

        Assert.That(args["key"], Is.EqualTo("value"));
        Assert.That(args["stray"], Is.Null);
        Assert.That(args["stray2"], Is.Null);
    }

    [Test]
    public void AValueIsTakenOnlyOnceByTheKeyBeforeIt()
    {
        var args = new CmdArgumentsInterpreter(s_keyOneTwoArguments);

        Assert.That(args["key"], Is.EqualTo("one"));
        Assert.That(args["two"], Is.Null);
    }

    [TestCase("/key", "first", "/key", "second")]
    [TestCase("/key=first", "/key=second")]
    [TestCase("/key=first", "/key", "second")]
    [TestCase("/key", "first", "/key=second")]
    public void WhenAKeyRepeatsTheFirstValueWins(params string[] arguments)
    {
        var args = new CmdArgumentsInterpreter(arguments);

        Assert.That(args["key"], Is.EqualTo("first"));
    }

    [Test]
    public void ARepeatedFlagKeepsTheFirstValue()
    {
        var args = new CmdArgumentsInterpreter(s_keyFirstKeyOtherArguments);

        Assert.That(args["key"], Is.EqualTo("first"));
    }

    [Test]
    public void ATrailingRepeatedFlagKeepsTheFirstValue()
    {
        var args = new CmdArgumentsInterpreter(s_keyFirstKeyArguments);

        Assert.That(args["key"], Is.EqualTo("first"));
    }

    [Test]
    public void AFlagRepeatedBeforeAnEnclosedValueKeepsTheFirstValue()
    {
        var args = new CmdArgumentsInterpreter(s_keyFirstKeyOtherXArguments);

        Assert.That(args["key"], Is.EqualTo("first"));
        Assert.That(args["other"], Is.EqualTo("x"));
    }

    [Test]
    public void KeysAreLookedUpCaseInsensitively()
    {
        var args = new CmdArgumentsInterpreter(s_resetposArguments);

        Assert.That(args["resetpos"], Is.EqualTo("true"));
        Assert.That(args["RESETPOS"], Is.EqualTo("true"));
    }

    [Test]
    public void AKeyThatWasNotGivenIsNull()
    {
        var args = new CmdArgumentsInterpreter(s_keyArguments);

        Assert.That(args["other"], Is.Null);
    }

    [Test]
    public void NoArgumentsGiveNoKeys()
    {
        var args = new CmdArgumentsInterpreter(Array.Empty<string>());

        Assert.That(args["anything"], Is.Null);
    }

    [Test]
    public void ArgumentsWithoutAWaitingKeyAreNotAnError()
    {
        using var recorder = new MessageRecorder();

        _ = new CmdArgumentsInterpreter(s_keyValueStrayArguments);

        Assert.That(recorder.Messages, Is.Empty);
    }

    [Test]
    public void ANullArgumentIsReportedAndNotThrown()
    {
        using var recorder = new MessageRecorder();
        CmdArgumentsInterpreter? args = null;

        // The null the signature rules out is the point: a broken argv must be reported, not thrown.
        Assert.DoesNotThrow(() => args = new CmdArgumentsInterpreter(new string[] { null! }));

        Assert.That(args, Is.Not.Null);
        Assert.That(args!["anything"], Is.Null);
        Assert.That(recorder.Matching(MessageClass.ErrorMsg, "Creating new Args failed"), Has.Count.EqualTo(1));
    }
}
