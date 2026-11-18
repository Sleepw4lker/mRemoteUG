using mRemoteUG.App;

using NUnit.Framework;

namespace mRemoteUG.Tests.App;

/// <summary>
/// The ID that keeps two <c>--selftest</c> runs on one host from sharing a registry key and a
/// report. That the running self-test actually uses these names is checked by running two at once,
/// which needs the real executable and is not a unit test.
/// </summary>
[TestFixture]
public class SelfTestRunIdTests
{
    private static readonly string[] s_selftestArguments = { "--selftest" };
    private static readonly string[] s_selftestIdRun7Arguments = { "--SELFTEST-ID", "run7" };
    private static readonly string[] s_selftestSelftestIdArguments = { "--selftest", "--selftest-id" };

    private const int ProcessId = 4242;

    [Test]
    public void WithoutTheSwitchTheProcessIdIsTheId()
    {
        bool ok = SelfTestRunId.TryParse(s_selftestArguments, ProcessId, out string id, out string? error);

        Assert.That(ok, Is.True);
        Assert.That(id, Is.EqualTo("4242"));
        Assert.That(error, Is.Null);
    }

    [Test]
    public void NoArgumentsAtAllAlsoFallBackToTheProcessId()
    {
        bool ok = SelfTestRunId.TryParse(null, ProcessId, out string id, out string? error);

        Assert.That(ok, Is.True);
        Assert.That(id, Is.EqualTo("4242"));
        Assert.That(error, Is.Null);
    }

    [TestCase("12345-build")]
    [TestCase("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [TestCase("a_B")]
    // The ends of each ASCII range, as the first character and inside the ID.
    [TestCase("z")]
    [TestCase("A")]
    [TestCase("Z")]
    [TestCase("0z-A_Z")]
    public void TheValueAfterTheSwitchIsTheId(string requested)
    {
        bool ok = SelfTestRunId.TryParse(
            new[] { "--selftest", "--selftest-id", requested, "--largefont" }, ProcessId, out string id, out _);

        Assert.That(ok, Is.True);
        Assert.That(id, Is.EqualTo(requested));
    }

    [Test]
    public void TheSwitchIsMatchedWithoutRegardToCase()
    {
        SelfTestRunId.TryParse(s_selftestIdRun7Arguments, ProcessId, out string id, out _);

        Assert.That(id, Is.EqualTo("run7"));
    }

    [Test]
    public void TheSwitchWithoutAValueIsRefusedAndTheProcessIdUsed()
    {
        bool ok = SelfTestRunId.TryParse(
            s_selftestSelftestIdArguments, ProcessId, out string id, out string? error);

        Assert.That(ok, Is.False);
        Assert.That(id, Is.EqualTo("4242"));
        Assert.That(error, Does.Contain("without an ID"));
    }

    [TestCase(@"..\Run")]
    [TestCase("a/b")]
    [TestCase(@"a\b")]
    [TestCase("a.log")]
    [TestCase("with space")]
    [TestCase("--largefont")]
    [TestCase("")]
    [TestCase("ü")]
    public void AnUnsafeIdIsRefusedAndTheProcessIdUsed(string requested)
    {
        bool ok = SelfTestRunId.TryParse(
            new[] { "--selftest", "--selftest-id", requested }, ProcessId, out string id, out string? error);

        Assert.That(ok, Is.False);
        Assert.That(id, Is.EqualTo("4242"));
        Assert.That(error, Does.Contain("not a valid"));
    }

    [Test]
    public void AnIdIsLimitedInLength()
    {
        Assert.That(SelfTestRunId.IsValid(new string('a', SelfTestRunId.MaxLength)), Is.True);
        Assert.That(SelfTestRunId.IsValid(new string('a', SelfTestRunId.MaxLength + 1)), Is.False);
        Assert.That(SelfTestRunId.IsValid(null), Is.False);
    }

    [Test]
    public void TheRegistryKeyIsTheRunsOwnSubkey()
    {
        Assert.That(SelfTestRunId.RegistryPath("12345-build"), Is.EqualTo(@"Software\mRemoteUG\SelfTest\12345-build"));
    }

    [Test]
    public void TheReportIsNamedAfterTheRun()
    {
        Assert.That(SelfTestRunId.ReportFileName("12345-build"), Is.EqualTo("mRemoteUG-selftest-12345-build.log"));
    }
}
