using Contry.Domain.Users;

namespace Contry.Domain.Ranked;

public sealed class RankedClueUsageStat
{
    public Guid UserId { get; set; }

    public string ClueId { get; set; } = null!;

    public int UsageCount { get; set; }

    public User User { get; set; } = null!;
}
