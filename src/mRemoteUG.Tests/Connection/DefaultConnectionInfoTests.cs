using System.Configuration;
using System.Reflection;

using mRemoteUG.Connection;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;


namespace mRemoteUG.Tests.Connection;

public class DefaultConnectionInfoTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionInfo _randomizedConnectionInfo = null!;

    [SetUp]
    public void Setup()
    {
        _randomizedConnectionInfo = ConnectionInfoHelpers.GetRandomizedConnectionInfo();
    }

    [TestCaseSource(nameof(GetConnectionInfoProperties))]
    public void LoadingDefaultInfoUpdatesAllProperties(PropertyInfo property)
    {
        DefaultConnectionInfo.Instance.LoadFrom(_randomizedConnectionInfo);
        object? valueInDestination = property.GetValue(DefaultConnectionInfo.Instance);
        object? valueInSource = property.GetValue(_randomizedConnectionInfo);
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    [TestCaseSource(nameof(GetConnectionInfoProperties))]
    public void SavingDefaultConnectionInfoExportsAllProperties(PropertyInfo property)
    {
        var saveTarget = new ConnectionInfo();
        object? randomizedValue = property.GetValue(_randomizedConnectionInfo);
        property.SetValue(DefaultConnectionInfo.Instance, randomizedValue);
        DefaultConnectionInfo.Instance.SaveTo(saveTarget);
        object? valueInDestination = property.GetValue(saveTarget);
        object? valueInSource = property.GetValue(DefaultConnectionInfo.Instance);
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    [TestCaseSource(nameof(GetConnectionInfoProperties))]
    public void CanSaveDefaultConnectionToModelWithAllStringProperties(PropertyInfo property)
    {
        var saveTarget = new SerializableConnectionInfoAllPropertiesOfType<string>();

        // randomize default connnection values to ensure we dont get false passing tests
        object? randomizedValue = property.GetValue(_randomizedConnectionInfo);
        property.SetValue(DefaultConnectionInfo.Instance, randomizedValue);

        DefaultConnectionInfo.Instance.SaveTo(saveTarget);

        string? valueInSource = property.GetValue(DefaultConnectionInfo.Instance)?.ToString();
        PropertyInfo targetProperty = saveTarget.GetType().GetProperty(property.Name)
                                      ?? throw new AssertionException($"the model has no {property.Name}");
        string? valueInDestination = targetProperty.GetValue(saveTarget)?.ToString();
        Assert.That(valueInDestination, Is.EqualTo(valueInSource));
    }

    // Startup and the options dialog load and save the default connection through
    // Settings.Default with a "ConDefault" prefix, by reflection. A property with no matching
    // setting is skipped with only a logged error, so its default is never loaded or saved.
    [TestCaseSource(nameof(GetConnectionInfoProperties))]
    public void EveryConnectionPropertyHasAUserSetting(PropertyInfo property)
    {
        string settingName = "ConDefault" + property.Name;

        PropertyInfo? setting = typeof(Settings).GetProperty(settingName);

        Assert.That(setting, Is.Not.Null, $"Settings has no {settingName}");
        Assert.That(setting!.GetCustomAttribute<UserScopedSettingAttribute>(), Is.Not.Null,
            $"{settingName} is not user-scoped, so it cannot be saved");
    }

    private static IEnumerable<PropertyInfo> GetConnectionInfoProperties()
    {
        return new ConnectionInfo().GetSerializableProperties();
    }
}
