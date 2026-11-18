using System.Configuration;
using System.Reflection;

using mRemoteUG.Connection;

using NUnit.Framework;


namespace mRemoteUG.Tests.Connection;

public class DefaultConnectionInheritanceTests
{
    [TestCaseSource(nameof(GetInheritanceProperties))]
    public void LoadingDefaultInheritanceUpdatesAllProperties(PropertyInfo property)
    {
        var inheritanceSource = new ConnectionInfoInheritance(new object(), true);
        inheritanceSource.TurnOnInheritanceCompletely();
        DefaultConnectionInheritance.Instance.TurnOffInheritanceCompletely();

        DefaultConnectionInheritance.Instance.LoadFrom(inheritanceSource);

        object? valueInDestination = property.GetValue(DefaultConnectionInheritance.Instance);
        object? valueInSource = property.GetValue(inheritanceSource);
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    [TestCaseSource(nameof(GetInheritanceProperties))]
    public void SavingDefaultInheritanceExportsAllProperties(PropertyInfo property)
    {
        var saveTarget = new ConnectionInfoInheritance(new object(), true);
        saveTarget.TurnOffInheritanceCompletely();
        DefaultConnectionInheritance.Instance.TurnOnInheritanceCompletely();

        DefaultConnectionInheritance.Instance.SaveTo(saveTarget);

        object? valueInDestination = property.GetValue(saveTarget);
        object? valueInSource = property.GetValue(DefaultConnectionInheritance.Instance);
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    // Startup and the options dialog load and save the default inheritance through
    // Settings.Default with an "InhDefault" prefix, by reflection. A property with no matching
    // setting is skipped with only a logged error, so its default is never loaded or saved.
    [TestCaseSource(nameof(GetInheritanceProperties))]
    public void EveryInheritancePropertyHasAUserSetting(PropertyInfo property)
    {
        string settingName = "InhDefault" + property.Name;

        PropertyInfo? setting = typeof(Settings).GetProperty(settingName);

        Assert.That(setting, Is.Not.Null, $"Settings has no {settingName}");
        Assert.That(setting!.PropertyType, Is.EqualTo(property.PropertyType));
        Assert.That(setting.GetCustomAttribute<UserScopedSettingAttribute>(), Is.Not.Null,
            $"{settingName} is not user-scoped, so it cannot be saved");
    }

    [Test]
    public void NewInheritanceInstancesCreatedWithDefaultInheritanceValues()
    {
        DefaultConnectionInheritance.Instance.Domain = true;
        var inheritanceInstance = new ConnectionInfoInheritance(new object());
        Assert.That(inheritanceInstance.Domain, Is.True);
    }

    [TestCaseSource(nameof(GetInheritanceProperties))]
    public void NewInheritanceInstancesCreatedWithAllDefaultInheritanceValues(PropertyInfo property)
    {
        DefaultConnectionInheritance.Instance.TurnOnInheritanceCompletely();
        var inheritanceInstance = new ConnectionInfoInheritance(new object());

        object? valueInDestination = property.GetValue(inheritanceInstance);
        object? valueInSource = property.GetValue(DefaultConnectionInheritance.Instance);
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    private static IEnumerable<PropertyInfo> GetInheritanceProperties()
    {
        return new ConnectionInfoInheritance(new object(), true).GetProperties();
    }
}
