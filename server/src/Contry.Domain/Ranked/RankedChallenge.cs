namespace Contry.Domain.Ranked;

public sealed class RankedChallenge
{
    public Guid Id { get; set; }

    public DateOnly ChallengeDateUtc { get; set; }

    public string TargetCountryId { get; set; } = string.Empty;

    public string ClueSetJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<RankedSession> Sessions { get; set; } = [];
}
