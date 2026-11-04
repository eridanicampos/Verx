using CashFlow.Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Identity.Api.Infrastructure;

public sealed class SeedUserOptions
{
    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

internal static class UserSeeder
{
    public const int MinPasswordLength = 12;

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct)
    {
        var seeds = configuration.GetSection("Identity:SeedUsers").Get<List<SeedUserOptions>>() ?? [];
        if (seeds.Count == 0)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        foreach (var seed in seeds.Where(s => !string.IsNullOrWhiteSpace(s.Username)))
        {
            if (!Roles.IsValid(seed.Role))
            {
                throw new InvalidOperationException($"Papel inválido para seed '{seed.Username}': {seed.Role}.");
            }

            if (seed.Password.Length < MinPasswordLength)
            {
                throw new InvalidOperationException($"Senha do usuário seed '{seed.Username}' ausente ou com menos de {MinPasswordLength} caracteres.");
            }

            var username = seed.Username.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Username == username, ct))
            {
                continue;
            }

            var now = clock.GetUtcNow();
            var user = User.Create(Guid.CreateVersion7(now), username, seed.Role, now);
            user.SetPasswordHash(hasher.HashPassword(user, seed.Password));
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
    }
}
