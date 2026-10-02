namespace HobbiesClub.Core.Interfaces;

public interface IPasswordHasher
{
    /// <summary>Hash of a password or PIN and its random salt (both Base64).</summary>
    (string Hash, string Salt) Hash(string secret);

    bool Verify(string secret, string hash, string salt);
}
