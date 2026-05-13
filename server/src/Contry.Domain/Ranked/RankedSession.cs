using Contry.Domain.Users;

namespace Contry.Domain.Ranked;

public sealed class RankedSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid RankedChallengeId { get; set; }

    public RankedSessionStatus Status { get; set; } = RankedSessionStatus.Playing;

    public int GuessCount { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public User User { get; set; } = null!;

    public RankedChallenge RankedChallenge { get; set; } = null!;

    public List<RankedGuess> Guesses { get; set; } = [];

    public bool IsCompleted => Status is RankedSessionStatus.Won or RankedSessionStatus.Lost;
}
