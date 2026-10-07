#nullable enable
using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using mRemoteUG.Security.SymmetricEncryption;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Security
{
    public class EncryptedSecureString
    {
        private static SecureString? _machineKey;
        private SecureString _secureString;
        private readonly ICryptographyProvider _cryptographyProvider;

        private static SecureString MachineKey
        {
            get { return _machineKey ??= GenerateNewMachineKey(32); }
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
            var encryptedText = _secureString.ConvertToUnsecureString();
            // The machine key is generated once per process and is the same key SetValue
            // encrypted with, so this does not fail in practice; an unset value decrypts to
            // empty either way.
            return _cryptographyProvider.Decrypt(encryptedText, MachineKey) ?? string.Empty;
        }

        public void SetValue(string value)
        {
            var cipherText = _cryptographyProvider.Encrypt(value, MachineKey);
            _secureString = cipherText.ConvertToSecureString();
        }

        private static SecureString GenerateNewMachineKey(int keySize)
        {
            var machineKeyString = new StringBuilder();
            {
                // RandomNumberGenerator.Fill replaces RNGCryptoServiceProvider, which is
                // obsolete (SYSLIB0023). Same randomness, and static, so there is nothing to
                // dispose. The modulo bias is left as it was: this key never leaves the
                // process and changing how it is generated is a separate decision.
                var buffer = new byte[4];
                for (var x = 0; x < keySize; x++)
                {
                    RandomNumberGenerator.Fill(buffer);
                    var value = 33 + (int)(BitConverter.ToUInt32(buffer, 0) % (126 - 33));
                    machineKeyString.Append((char)value);
                }
            }

            return machineKeyString.ToString().ConvertToSecureString();
        }
    }
}