namespace mRemoteUG.Config.Serializers
{
    public interface ISerializer<in TIn, out TOut>
    {
        TOut Serialize(TIn model);
    }
}