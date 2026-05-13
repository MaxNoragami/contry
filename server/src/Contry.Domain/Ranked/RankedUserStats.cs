using Contry.Domain.Users;

namespace Contry.Domain.Ranked;

public sealed class RankedUserStats
{
    public Guid UserId { get; set; }

    public int PlayedCount { get; set; }

    public int WonCount { get; set; }

    public int TotalGuessesOnWins { get; set; }

    public int? FastestWinGuessCount { get; set; }

    public int? SlowestWinGuessCount { get; set; }

    public int CurrentStreak { get; set; }

    public int BestStreak { get; set; }

    public DateOnly? LastCompletedChallengeDateUtc { get; set; }

    public User User { get; set; } = null!;
}
