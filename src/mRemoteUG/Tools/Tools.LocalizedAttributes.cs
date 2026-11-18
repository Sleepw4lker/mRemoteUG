using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Tools;

public class LocalizedAttributes
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedCategoryAttribute : CategoryAttribute
    {
        private const int MaxOrder = 10;
        private readonly int _order;

        public LocalizedCategoryAttribute(string value, int order = 1) : base(value)
        {
            this._order = order > MaxOrder ? MaxOrder : order;
        }

        protected override string GetLocalizedString(string value)
        {
            string orderPrefix = "";
            for (int x = 0; x <= MaxOrder - _order; x++)
            {
                orderPrefix += Convert.ToString("\t");
            }

            return orderPrefix + Language.ResourceManager.GetString(value, CultureInfo.CurrentUICulture);
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {

        private bool _localized;

        public LocalizedDisplayNameAttribute(string value) : base(value)
        {
            _localized = false;
        }

        public override string DisplayName
        {
            get
            {
                if (!_localized)
                {
                    _localized = true;
                    // A name with no resource used to come back as null; the grid draws that and
                    // an empty string alike.
                    DisplayNameValue =
                        Language.ResourceManager.GetString(DisplayNameValue, CultureInfo.CurrentUICulture)
                        ?? string.Empty;
                }

                return base.DisplayName;
            }
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private bool _localized;
        public LocalizedDescriptionAttribute(string value) : base(value)
        {
            _localized = false;
        }

        public override string Description
        {
            get
            {
                if (!_localized)
                {
                    _localized = true;
                    // As for the display name: a missing resource reads as empty, not null.
                    DescriptionValue =
                        Language.ResourceManager.GetString(DescriptionValue, CultureInfo.CurrentUICulture)
                        ?? string.Empty;
                }

                return base.Description;
            }
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedDefaultValueAttribute : DefaultValueAttribute
    {
        public LocalizedDefaultValueAttribute(string name)
            : base(Language.ResourceManager.GetString(name, CultureInfo.CurrentUICulture))
        {
        }

        // This allows localized attributes in a derived class to override a matching
        // non-localized attribute inherited from its base class
        public override object TypeId
        {
            get { return typeof(DefaultValueAttribute); }
        }
    }

    #region Special localization - with String.Format
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedDisplayNameInheritAttribute : DisplayNameAttribute
    {
        private bool _localized;
        public LocalizedDisplayNameInheritAttribute(string value) : base(value)
        {

            _localized = false;
        }

        [SuppressMessage("Performance", "CA1863",
                         Justification = "The format is a localized resource read at run time; "
                                         + "a cached CompositeFormat would freeze the UI language.")]
        public override string DisplayName
        {
            get
            {
                if (!_localized)
                {
                    _localized = true;
                    DisplayNameValue = string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strFormatInherit,
                        Language.ResourceManager.GetString(DisplayNameValue, CultureInfo.CurrentUICulture));
                }

                return base.DisplayName;
            }
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class LocalizedDescriptionInheritAttribute : DescriptionAttribute
    {
        private bool _localized;
        public LocalizedDescriptionInheritAttribute(string value) : base(value)
        {

            _localized = false;
        }

        [SuppressMessage("Performance", "CA1863",
                         Justification = "The format is a localized resource read at run time; "
                                         + "a cached CompositeFormat would freeze the UI language.")]
        public override string Description
        {
            get
            {
                if (!_localized)
                {
                    _localized = true;
                    DescriptionValue = string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strFormatInheritDescription,
                        Language.ResourceManager.GetString(DescriptionValue, CultureInfo.CurrentUICulture));
                }

                return base.Description;
            }
        }
    }
    #endregion
}
