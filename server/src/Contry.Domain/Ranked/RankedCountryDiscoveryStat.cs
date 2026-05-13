using Contry.Domain.Users;

namespace Contry.Domain.Ranked;

public sealed class RankedCountryDiscoveryStat
{
    public Guid UserId { get; set; }

    public string CountryId { get; set; } = null!;

    public bool Discovered { get; set; }

    public int? BestAttempts { get; set; }

    public int SolvedCount { get; set; }

    public DateTimeOffset? LastSolvedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
