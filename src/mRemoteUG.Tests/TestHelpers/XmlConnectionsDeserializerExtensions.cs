using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Tree;

using NUnit.Framework;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// For tests that expect a file to load. <see cref="XmlConnectionsDeserializer.Deserialize(string)"/>
/// answers null when the file's password was not given, and such a test should fail saying so
/// rather than with a <see cref="NullReferenceException"/> further down.
/// </summary>
internal static class XmlConnectionsDeserializerExtensions
{
    public static ConnectionTreeModel DeserializeLoaded(this XmlConnectionsDeserializer deserializer, string xml)
    {
        return deserializer.Deserialize(xml)
               ?? throw new AssertionException("The file did not load: no password was given for it.");
    }
}
