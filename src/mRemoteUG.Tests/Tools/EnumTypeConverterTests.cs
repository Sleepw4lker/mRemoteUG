using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

public class EnumTypeConverterTests
{
    private enum Sample
    {
        [System.ComponentModel.Description("The described one")]
        Described = 1,
        Plain = 2
    }

    private readonly MiscTools.EnumTypeConverter _converter = new(typeof(Sample));

    [Test]
    public void AMemberWithADescriptionIsShownAsIt()
    {
        object? shown = _converter.ConvertTo(null, null, Sample.Described, typeof(string));
        Assert.That(shown, Is.EqualTo("The described one"));
    }

    [Test]
    public void AMemberWithoutADescriptionIsShownByName()
    {
        Assert.That(_converter.ConvertTo(null, null, Sample.Plain, typeof(string)), Is.EqualTo("Plain"));
    }

    /// <remarks>
    /// Enum.GetName answers null for a value no member names, and GetField(null) threw
    /// ArgumentNullException out of the property grid.
    /// </remarks>
    [Test]
    public void AValueNoMemberNamesIsShownAsEnumToStringShowsIt()
    {
        Assert.That(_converter.ConvertTo(null, null, (Sample)7, typeof(string)), Is.EqualTo("7"));
    }
}
