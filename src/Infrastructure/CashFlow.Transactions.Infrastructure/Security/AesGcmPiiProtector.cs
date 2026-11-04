using System.Security.Cryptography;
using System.Text;
using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.Security;

public sealed class AesGcmPiiProtector : IPiiProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys;
    private readonly byte[] _blindIndexKey;

    public AesGcmPiiProtector(IOptions<PiiOptions> options)
    {
        var o = options.Value;
        _activeKeyId = o.ActiveKeyId;
        _keys = o.EncryptionKeys.ToDictionary(k => k.Key, k => DecodeKey(k.Value, $"Pii:EncryptionKeys:{k.Key}"), StringComparer.Ordinal);
        _blindIndexKey = DecodeKey(o.BlindIndexKey, "Pii:BlindIndexKey");

        if (!_keys.ContainsKey(_activeKeyId))
        {
            throw new InvalidOperationException($"Chave PII ativa '{_activeKeyId}' não configurada.");
        }
    }

    public string Encrypt(string plainText)
    {
        var key = _keys[_activeKeyId];
        var plain = Encoding.UTF8.GetBytes(plainText);
        var buffer = new byte[NonceSize + TagSize + plain.Length];
        var nonce = buffer.AsSpan(0, NonceSize);
        var tag = buffer.AsSpan(NonceSize, TagSize);
        var cipher = buffer.AsSpan(NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(_activeKeyId));
        return $"{_activeKeyId}:{Convert.ToBase64String(buffer)}";
    }

    public string Decrypt(string cipherText)
    {
        var separator = cipherText.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new CryptographicException("Formato de dado protegido inválido.");
        }

        var keyId = cipherText[..separator];
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException($"Chave '{keyId}' indisponível para descriptografia.");
        }

        var buffer = Convert.FromBase64String(cipherText[(separator + 1)..]);
        var plain = new byte[buffer.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            buffer.AsSpan(0, NonceSize),
            buffer.AsSpan(NonceSize + TagSize),
            buffer.AsSpan(NonceSize, TagSize),
            plain,
            Encoding.UTF8.GetBytes(keyId));
        return Encoding.UTF8.GetString(plain);
    }

    public string BlindIndex(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_blindIndexKey, Encoding.UTF8.GetBytes(value)));

    private static byte[] DecodeKey(string base64, string name)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException($"Segredo obrigatório ausente: {name}.");
        }

        var key = Convert.FromBase64String(base64.Trim());
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"{name} deve ter 256 bits (32 bytes em Base64).");
        }

        return key;
    }
}
