#nullable enable
namespace mRemoteUG.Security
{
    public interface ICryptoProviderFactory
    {
        ICryptographyProvider Build();
    }
}