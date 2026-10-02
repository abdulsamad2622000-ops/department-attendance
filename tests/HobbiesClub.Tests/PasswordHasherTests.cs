using HobbiesClub.Services;

namespace HobbiesClub.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Correct_secret_verifies()
    {
        var (hash, salt) = _hasher.Hash("Secret#123");
        Assert.True(_hasher.Verify("Secret#123", hash, salt));
    }

    [Fact]
    public void Wrong_secret_fails()
    {
        var (hash, salt) = _hasher.Hash("Secret#123");
        Assert.False(_hasher.Verify("secret#123", hash, salt));
    }

    [Fact]
    public void Same_secret_gives_different_hash_each_time()
    {
        var first = _hasher.Hash("1234");
        var second = _hasher.Hash("1234");
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.NotEqual(first.Salt, second.Salt);
    }
}
