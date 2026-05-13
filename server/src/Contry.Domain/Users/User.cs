using Contry.Domain.Authentication;
using Contry.Domain.Ranked;

namespace Contry.Domain.Users;

public sealed class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<RefreshSession> RefreshSessions { get; set; } = [];

    public List<RankedSession> RankedSessions { get; set; } = [];

    public RankedUserStats? RankedUserStats { get; set; }
}
