using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.Api.Infrastructure;

public sealed class SigningKeyProvider
{
    private readonly RSA _rsa;

    public SigningKeyProvider(IOptions<JwtOptions> options, ILogger<SigningKeyProvider> logger)
    {
        var o = options.Value;
        _rsa = RSA.Create();

        if (!string.IsNullOrWhiteSpace(o.SigningKeyPem))
        {
            _rsa.ImportFromPem(o.SigningKeyPem);
        }
        else if (o.AllowEphemeralKey)
        {
            _rsa.KeySize = 2048;
            logger.LogWarning("Usando chave de assinatura EFÊMERA (somente desenvolvimento). Tokens não sobrevivem a reinícios.");
        }
        else
        {
            throw new InvalidOperationException("Jwt:SigningKeyPem não configurado. Gere com scripts/generate-secrets.");
        }

        if (_rsa.KeySize < 2048)
        {
            throw new InvalidOperationException("A chave RSA deve ter no mínimo 2048 bits.");
        }

        var key = new RsaSecurityKey(_rsa);
        key.KeyId = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        SecurityKey = key;
        Credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public RsaSecurityKey SecurityKey { get; }

    public SigningCredentials Credentials { get; }

    public JsonWebKey PublicJwk()
    {
        var publicKey = new RsaSecurityKey(_rsa.ExportParameters(includePrivateParameters: false)) { KeyId = SecurityKey.KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return jwk;
    }
}
