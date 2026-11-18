namespace mRemoteUG.Tree;

public interface ITreeNodeClickHandler<in T>
{
    void Execute(T clickedNode);
}
