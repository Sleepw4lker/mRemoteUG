using System.ComponentModel;

using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

/// <summary>
/// The attributes that label and order the property grid. The grid sorts categories by name, so
/// the leading tabs are what order them: a lower order gets more tabs and sorts first.
/// </summary>
[TestFixture]
public class LocalizedAttributesTests
{
    private const string CategoryKey = "strCategoryDisplay";
    private const string NameKey = "strPropertyNameName";
    private const string DescriptionKey = "strPropertyNameDescription";

    /// <remarks>
    /// Order 11 is past the highest order there is and is treated as 10, so it sorts with it
    /// rather than ahead of everything with zero tabs.
    /// </remarks>
    [TestCase(1, 10)]
    [TestCase(10, 1)]
    [TestCase(11, 1)]
    public void ACategoryIsPrefixedWithOneTabMorePerOrderBelowTheHighest(int order, int tabs)
    {
        var attribute = new LocalizedAttributes.LocalizedCategoryAttribute(CategoryKey, order);

        Assert.That(attribute.Category, Is.EqualTo(new string('\t', tabs) + Language.strCategoryDisplay));
    }

    [Test]
    public void ADisplayNameIsTheLocalizedTextAndStaysItOnTheNextRead()
    {
        var attribute = new LocalizedAttributes.LocalizedDisplayNameAttribute(NameKey);

        Assert.Multiple(() =>
        {
            Assert.That(attribute.DisplayName, Is.EqualTo(Language.strPropertyNameName));
            Assert.That(attribute.DisplayName, Is.EqualTo(Language.strPropertyNameName),
                        "The second read localized the already localized text again.");
        });
    }

    [Test]
    public void ADescriptionIsTheLocalizedTextAndStaysItOnTheNextRead()
    {
        var attribute = new LocalizedAttributes.LocalizedDescriptionAttribute(DescriptionKey);

        Assert.Multiple(() =>
        {
            Assert.That(attribute.Description, Is.EqualTo(Language.strPropertyNameDescription));
            Assert.That(attribute.Description, Is.EqualTo(Language.strPropertyNameDescription),
                        "The second read localized the already localized text again.");
        });
    }

    /// <summary>
    /// The shared TypeId is what lets a localized default value on a derived class replace a
    /// plain [DefaultValue] inherited from its base, rather than sitting beside it.
    /// </summary>
    [Test]
    public void ALocalizedDefaultValueTakesThePlaceOfAPlainOne()
    {
        var attribute = new LocalizedAttributes.LocalizedDefaultValueAttribute("strConnections");

        Assert.That(attribute.TypeId, Is.EqualTo(typeof(DefaultValueAttribute)));
    }
}
