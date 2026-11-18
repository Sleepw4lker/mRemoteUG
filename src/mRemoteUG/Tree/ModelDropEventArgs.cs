using System.Collections;

namespace mRemoteUG.Tree;

/// <summary>
/// Lets the drop handler report back how a drag should be drawn. Replaces the
/// feedback half of ObjectListView's SimpleDropSink.
/// </summary>
public interface IDropFeedback
{
    bool EnableFeedback { get; set; }
    Color FeedbackColor { get; set; }
}

/// <summary>
/// Describes a drag over, or a drop onto, the connection tree. Mirrors the members
/// of ObjectListView's ModelDropEventArgs that mRemoteUG actually used, so the drop
/// logic and its tests did not have to change.
/// </summary>
public class ModelDropEventArgs : EventArgs
{
    /// <summary>The model under the cursor, or null over empty space.</summary>
    public object? TargetModel { get; set; }

    /// <summary>The models being dragged.</summary>
    public required IList SourceModels { get; set; }

    public DropTargetLocation DropTargetLocation { get; set; }

    public DragDropEffects Effect { get; set; }

    /// <summary>Explains why a drop is refused, drawn next to the target row.</summary>
    public string? InfoMessage { get; set; }

    public bool Handled { get; set; }

    public required IDropFeedback DropSink { get; set; }
}
