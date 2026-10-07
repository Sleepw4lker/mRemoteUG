using mRemoteUG.Container;

namespace mRemoteUG.Config.Import
{
    public interface IConnectionImporter<in TSource>
        where TSource : class
    {
        void Import(TSource source, ContainerInfo destinationContainer);
    }
}