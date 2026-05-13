namespace Contry.Domain.Ranked;

public sealed class RankedGuess
{
    public Guid Id { get; set; }

    public Guid RankedSessionId { get; set; }

    public int AttemptNumber { get; set; }

    public string GuessCountryId { get; set; } = string.Empty;

    public string GuessCountryName { get; set; } = string.Empty;

    public string ResultsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public RankedSession RankedSession { get; set; } = null!;
}
