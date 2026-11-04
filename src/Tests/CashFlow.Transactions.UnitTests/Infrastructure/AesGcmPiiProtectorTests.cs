using System.Security.Cryptography;
using CashFlow.Transactions.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.UnitTests.Infrastructure;

public sealed class AesGcmPiiProtectorTests
{
    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmPiiProtector Create(string activeKeyId, Dictionary<string, string> keys, string? blind = null) =>
        new(Options.Create(new PiiOptions { ActiveKeyId = activeKeyId, EncryptionKeys = keys, BlindIndexKey = blind ?? Key() }));

    [Fact]
    public void Roundtrip_and_ciphertext_is_randomized()
    {
        var sut = Create("k1", new() { ["k1"] = Key() });

        var a = sut.Encrypt("12345678909");
        var b = sut.Encrypt("12345678909");

        a.ShouldNotBe(b);
        a.ShouldStartWith("k1:");
        a.ShouldNotContain("12345678909");
        sut.Decrypt(a).ShouldBe("12345678909");
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var sut = Create("k1", new() { ["k1"] = Key() });
        var bytes = Convert.FromBase64String(sut.Encrypt("secret")[3..]);
        bytes[^1] ^= 0xFF;

        Should.Throw<AuthenticationTagMismatchException>(() => sut.Decrypt("k1:" + Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Key_rotation_keeps_old_data_readable()
    {
        var k1 = Key();
        var blind = Key();
        var old = Create("k1", new() { ["k1"] = k1 }, blind).Encrypt("doc");

        var rotated = Create("k2", new() { ["k1"] = k1, ["k2"] = Key() }, blind);

        rotated.Decrypt(old).ShouldBe("doc");
        rotated.Encrypt("doc").ShouldStartWith("k2:");
    }

    [Fact]
    public void Blind_index_is_deterministic_per_key()
    {
        var blind = Key();
        Create("k1", new() { ["k1"] = Key() }, blind).BlindIndex("x")
            .ShouldBe(Create("k1", new() { ["k1"] = Key() }, blind).BlindIndex("x"));
    }

    [Fact]
    public void Missing_or_weak_keys_fail_fast()
    {
        Should.Throw<InvalidOperationException>(() => Create("k1", new()));
        Should.Throw<InvalidOperationException>(() => Create("k1", new() { ["k1"] = Convert.ToBase64String(new byte[16]) }));
        Should.Throw<InvalidOperationException>(() => Create("k1", new() { ["k1"] = Key() }, blind: " "));
    }
}
