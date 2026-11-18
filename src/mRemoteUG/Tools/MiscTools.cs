using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security;

using mRemoteUG.UI.Forms;

using static System.String;

namespace mRemoteUG.Tools;

public static class MiscTools
{
    public static SecureString? PasswordDialog(string? passwordName = null, bool verify = true)
    {
        var passwordForm = new PasswordForm(passwordName, verify);
        return passwordForm.GetKey();
    }


    public static string LeadingZero(string number)
    {
        if (Convert.ToInt32(number, CultureInfo.InvariantCulture) < 10)
        {
            return "0" + number;
        }
        return number;
    }

    public static string GetExceptionMessageRecursive(Exception ex)
    {
        return GetExceptionMessageRecursive(ex, Environment.NewLine);
    }
    private static string GetExceptionMessageRecursive(Exception ex, string separator)
    {
        string message = ex.Message;
        if (ex.InnerException == null)
        {
            return message;
        }

        string innerMessage = GetExceptionMessageRecursive(ex.InnerException, separator);
        message = Join(separator, message, innerMessage);
        return message;
    }



    public class EnumTypeConverter : EnumConverter
    {
        private readonly Type _enumType;

        public EnumTypeConverter(Type type) : base(type)
        {
            _enumType = type;
        }

        public override bool CanConvertTo(
            ITypeDescriptorContext? context,
            [NotNullWhen(true)] Type? destinationType)
        {
            return destinationType == typeof(string);
        }

        public override object? ConvertTo(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object? value,
            Type destinationType)
        {
            if (value == null)
            {
                return null;
            }

            // A value no member names (an undefined number, a combination) has no description
            // to look up, so it is shown the way Enum.ToString shows it, like a member without one.
            string? name = Enum.GetName(_enumType, value);
            FieldInfo? fi = name == null ? null : _enumType.GetField(name);
            DescriptionAttribute? dna = fi == null
                ? null
                : (DescriptionAttribute?)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

            return dna != null ? dna.Description : value.ToString();
        }

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            foreach (FieldInfo fi in _enumType.GetFields())
            {
                var dna = (DescriptionAttribute?)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                if (dna != null && (string)value == dna.Description)
                {
                    return Enum.Parse(_enumType, fi.Name);
                }
            }

            return value != null ? Enum.Parse(_enumType, (string)value) : null;
        }
    }

    public class YesNoTypeConverter : TypeConverter
    {

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(
            ITypeDescriptorContext? context,
            [NotNullWhen(true)] Type? destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (!(value is string))
            {
                return base.ConvertFrom(context, culture, value);
            }

            if (string.Equals(value.ToString(), Language.strYes, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value.ToString(), Language.strNo, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new FormatException("Values must be \"Yes\" or \"No\"");
        }

        public override object? ConvertTo(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object? value,
            Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? Language.strYes : Language.strNo;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            bool[] bools = { true, false };

            var svc = new StandardValuesCollection(bools);

            return svc;
        }
    }
}
