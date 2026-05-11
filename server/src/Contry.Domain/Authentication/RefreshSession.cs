using Contry.Domain.Users;

namespace Contry.Domain.Authentication;

public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public DateTimeOffset? ReuseDetectedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public User User { get; set; } = null!;

    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc <= now;

    public bool IsRevoked => RevokedAtUtc.HasValue;
}
