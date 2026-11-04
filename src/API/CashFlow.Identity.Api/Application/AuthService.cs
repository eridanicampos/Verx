using CashFlow.Identity.Api.Domain;
using CashFlow.Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CashFlow.Identity.Api.Application;

public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public enum AuthFailure
{
    InvalidCredentials,
    LockedOut,
    InvalidRefreshToken,
    RefreshTokenReused,
}

public sealed record AuthResult(TokenPair? Tokens, AuthFailure? Failure)
{
    public static AuthResult Ok(TokenPair tokens) => new(tokens, null);

    public static AuthResult Fail(AuthFailure failure) => new(null, failure);
}

public sealed partial class AuthService
{
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(null!, "dummy-password-for-timing");

    private readonly IdentityDbContext _db;
    private readonly TokenService _tokens;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IOptions<JwtOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IdentityDbContext db,
        TokenService tokens,
        IPasswordHasher<User> hasher,
        IOptions<JwtOptions> options,
        TimeProvider clock,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, string? ip, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var normalized = username.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);

        if (user is null)
        {
            _hasher.VerifyHashedPassword(null!, DummyHash, password);
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            LogLockedOut(_logger, user.Id);
            return AuthResult.Fail(AuthFailure.LockedOut);
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.RegisterFailedLogin(now);
            await _db.SaveChangesAsync(ct);
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(_hasher.HashPassword(user, password));
        }

        user.RegisterSuccessfulLogin();
        var pair = IssuePair(user, Guid.CreateVersion7(now), ip, now);
        await _db.SaveChangesAsync(ct);
        return AuthResult.Ok(pair);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ip, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var hash = TokenService.Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        if (stored.RevokedAt is not null && stored.ReplacedById is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, "reuse-detected", ct);
            LogReuseDetected(_logger, stored.UserId, stored.FamilyId);
            return AuthResult.Fail(AuthFailure.RefreshTokenReused);
        }

        if (!stored.IsActive(now))
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || user.IsLockedOut(now))
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        var pair = IssuePair(user, stored.FamilyId, ip, now, out var newToken);
        stored.Revoke(now, "rotated", newToken.Id);
        await _db.SaveChangesAsync(ct);
        return AuthResult.Ok(pair);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, _clock.GetUtcNow(), "logout", ct);
        }
    }

    private TokenPair IssuePair(User user, Guid familyId, string? ip, DateTimeOffset now) =>
        IssuePair(user, familyId, ip, now, out _);

    private TokenPair IssuePair(User user, Guid familyId, string? ip, DateTimeOffset now, out RefreshToken entity)
    {
        var o = _options.Value;
        var (access, accessExpires) = _tokens.CreateAccessToken(user);
        var (refresh, refreshHash) = TokenService.CreateRefreshToken();
        entity = RefreshToken.Issue(user.Id, familyId, refreshHash, now, o.RefreshTokenLifetime, ip);
        _db.RefreshTokens.Add(entity);
        return new TokenPair(access, accessExpires, refresh, entity.ExpiresAt);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken ct)
    {
        var family = await _db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var token in family)
        {
            token.Revoke(now, reason);
        }

        await _db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login bloqueado (lockout) para usuário {UserId}")]
    private static partial void LogLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reuso de refresh token detectado. Usuário {UserId}, família {FamilyId} revogada")]
    private static partial void LogReuseDetected(ILogger logger, Guid userId, Guid familyId);
}
