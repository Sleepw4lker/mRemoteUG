using System;
using System.ComponentModel;
using System.Linq;
using mRemoteUG.Tools;

namespace mRemoteUG.Connection.Protocol
{
    /// <summary>
    /// Property grid converter for <see cref="ProtocolType"/>. Behaves like
    /// <see cref="MiscTools.EnumTypeConverter"/> but offers only the protocols this build
    /// supports, so the removed ones stay loadable without becoming selectable.
    /// </summary>
    public class ProtocolTypeConverter : MiscTools.EnumTypeConverter
    {
        public ProtocolTypeConverter(Type type) : base(type)
        {
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(ProtocolTypes.Supported.ToArray());
        }
    }
}
