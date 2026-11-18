using System.Diagnostics.CodeAnalysis;
using System.Security;

using mRemoteUG.Tools;

namespace mRemoteUG.Security.Authentication;

public class PasswordAuthenticator
{
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly string _cipherText;
    private readonly Func<SecureString?> _authenticationRequestor;

    public int MaxAttempts { get; set; } = 3;
    /// <summary>The password that worked, set only once one has.</summary>
    public SecureString? LastAuthenticatedPassword { get; private set; }

    public PasswordAuthenticator(
        ICryptographyProvider cryptographyProvider,
        string cipherText,
        Func<SecureString?> authenticationRequestor)
    {
        _cryptographyProvider = cryptographyProvider.ThrowIfNull(nameof(cryptographyProvider));
        _cipherText = cipherText.ThrowIfNullOrEmpty(nameof(cipherText));
        _authenticationRequestor = authenticationRequestor.ThrowIfNull(nameof(authenticationRequestor));
    }

    /// <remarks>
    /// Returning true is the guarantee that <see cref="LastAuthenticatedPassword"/> is set;
    /// XmlConnectionsDecryptor reads it straight after checking this, and the attribute is
    /// what lets it do so without a null check the bool has already made redundant.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(LastAuthenticatedPassword))]
    public bool Authenticate(SecureString password)
    {
        if (TryPassword(password))
        {
            return true;
        }

        // MaxAttempts counts the prompts, and each answer is tried before the next prompt.
        // The loop used to count the password it was given as the first attempt, so it asked
        // MaxAttempts times but returned before trying the last answer.
        for (int prompt = 0; prompt < MaxAttempts; prompt++)
        {
            // null is the user cancelling the prompt. The old Optional<T> folded a
            // null value into Empty, so the null check after unwrapping could never
            // fire; cancelled and empty are now told apart by the type.
            SecureString? providedPassword = _authenticationRequestor();
            if (providedPassword == null || providedPassword.Length == 0)
            {
                return false;
            }

            if (TryPassword(providedPassword))
            {
                return true;
            }
        }
        return false;
    }

    [MemberNotNullWhen(true, nameof(LastAuthenticatedPassword))]
    private bool TryPassword(SecureString password)
    {
        try
        {
            _cryptographyProvider.Decrypt(_cipherText, password);
        }
        catch (EncryptionException)
        {
            // A wrong password is expected here: the provider rejects it with this exception,
            // and the answer to that is to ask again, not to fail. Anything else is damaged
            // data no password can fix, so it escapes rather than ending in a prompt.
            return false;
        }

        LastAuthenticatedPassword = password;
        return true;
    }
}
