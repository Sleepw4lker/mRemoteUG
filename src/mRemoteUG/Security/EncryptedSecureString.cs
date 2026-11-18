using System.Security;
using System.Security.Cryptography;
using System.Text;

using mRemoteUG.Security.SymmetricEncryption;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Security;

public class EncryptedSecureString : IDisposable
{
    private static SecureString? s_machineKey;
    private SecureString _secureString;
    private readonly ICryptographyProvider _cryptographyProvider;

    private static SecureString MachineKey
    {
        get { return s_machineKey ??= GenerateNewMachineKey(32); }
    }

    public EncryptedSecureString()
    {
        _secureString = new SecureString();
        _cryptographyProvider = new AeadCryptographyProvider();
    }

    public EncryptedSecureString(ICryptographyProvider cryptographyProvider)
    {
        _secureString = new SecureString();
        _cryptographyProvider = cryptographyProvider;
    }

    public string GetClearTextValue()
    {
        string encryptedText = _secureString.ConvertToUnsecureString();
        // The machine key is generated once per process and is the same key SetValue
        // encrypted with, so this does not fail in practice; an unset value decrypts to
        // empty.
        return _cryptographyProvider.Decrypt(encryptedText, MachineKey);
    }

    public void SetValue(string value)
    {
        string cipherText = _cryptographyProvider.Encrypt(value, MachineKey);
        SecureString previous = _secureString;
        _secureString = cipherText.ConvertToSecureString();
        previous.Dispose();
    }

    public void Dispose()
    {
        _secureString.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static SecureString GenerateNewMachineKey(int keySize)
    {
        var machineKeyString = new StringBuilder();
        {
            // RandomNumberGenerator.Fill replaces RNGCryptoServiceProvider, which is
            // obsolete (SYSLIB0023). Same randomness, and static, so there is nothing to
            // dispose. The modulo bias is left as it was: this key never leaves the
            // process and changing how it is generated is a separate decision.
            byte[] buffer = new byte[4];
            for (int x = 0; x < keySize; x++)
            {
                RandomNumberGenerator.Fill(buffer);
                int value = 33 + (int)(BitConverter.ToUInt32(buffer, 0) % (126 - 33));
                machineKeyString.Append((char)value);
            }
        }

        return machineKeyString.ToString().ConvertToSecureString();
    }
}
