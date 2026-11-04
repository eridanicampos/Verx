namespace CashFlow.Identity.Api.Domain;

public sealed class User
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public int FailedAttempts { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(Guid id, string username, string role, DateTimeOffset now) => new()
    {
        Id = id,
        Username = username.Trim().ToLowerInvariant(),
        Role = role,
        CreatedAt = now,
    };

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is not null && LockoutEnd > now;

    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts)
        {
            LockoutEnd = now + LockoutDuration;
            FailedAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedAttempts = 0;
        LockoutEnd = null;
    }
}
