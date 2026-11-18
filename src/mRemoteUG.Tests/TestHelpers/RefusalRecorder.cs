using mRemoteUG.Config.Serializers.Xml;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// Replaces <see cref="XmlConnectionsDeserializer.ShowRefusal"/> while it is alive, recording
/// the heading of each refusal instead of showing the modal dialog, and puts the real one back
/// when disposed.
/// </summary>
public sealed class RefusalRecorder : IDisposable
{
    private readonly Action<string, string, string> _originalShowRefusal;
    private readonly List<string> _headings = new List<string>();

    public RefusalRecorder()
    {
        _originalShowRefusal = XmlConnectionsDeserializer.ShowRefusal;
        XmlConnectionsDeserializer.ShowRefusal = (heading, _, _) => _headings.Add(heading);
    }

    public IReadOnlyList<string> Headings => _headings;

    public void Dispose()
    {
        XmlConnectionsDeserializer.ShowRefusal = _originalShowRefusal;
    }
}
