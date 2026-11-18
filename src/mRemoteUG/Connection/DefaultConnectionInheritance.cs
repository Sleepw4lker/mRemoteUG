using System.Reflection;

using mRemoteUG.App;


namespace mRemoteUG.Connection;

public class DefaultConnectionInheritance : ConnectionInfoInheritance
{
    public static DefaultConnectionInheritance Instance { get; } = new DefaultConnectionInheritance();

    private DefaultConnectionInheritance() : base(null, true)
    {
    }

    static DefaultConnectionInheritance()
    { }


    public void LoadFrom<TSource>(TSource sourceInstance, Func<string, string>? propertyNameMutator = null)
    {
        if (propertyNameMutator == null)
        {
            propertyNameMutator = a => a;
        }

        IEnumerable<PropertyInfo> inheritanceProperties = GetProperties();
        foreach (PropertyInfo property in inheritanceProperties)
        {
            PropertyInfo? propertyFromSettings = typeof(TSource).GetProperty(propertyNameMutator(property.Name));
            if (propertyFromSettings == null)
            {
                Runtime.MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                    $"DefaultConInherit-LoadFrom: Could not load {property.Name}", true);
                continue;
            }
            object? valueFromSettings = propertyFromSettings.GetValue(sourceInstance, null);
            property.SetValue(Instance, valueFromSettings, null);
        }
    }

    public void SaveTo<TDestination>(TDestination destinationInstance, Func<string, string>? propertyNameMutator = null)
    {
        if (propertyNameMutator == null)
        {
            propertyNameMutator = a => a;
        }

        IEnumerable<PropertyInfo> inheritanceProperties = GetProperties();
        foreach (PropertyInfo property in inheritanceProperties)
        {
            PropertyInfo? propertyFromSettings = typeof(TDestination).GetProperty(propertyNameMutator(property.Name));
            object? localValue = property.GetValue(Instance, null);
            if (propertyFromSettings == null)
            {
                Runtime.MessageCollector?.AddMessage(Messages.MessageClass.ErrorMsg,
                    $"DefaultConInherit-SaveTo: Could not load {property.Name}", true);
                continue;
            }
            propertyFromSettings.SetValue(destinationInstance, localValue, null);
        }
    }
}
