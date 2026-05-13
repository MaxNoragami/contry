using Contry.Domain.Ranked;

namespace Contry.Application.Ranked;

public interface IRankedStore
{
    Task<RankedChallenge?> FindChallengeByDateAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken);

    Task AddChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken);

    Task<RankedSession?> FindSessionByUserAndDateAsync(Guid userId, DateOnly challengeDateUtc, bool includeGuesses, CancellationToken cancellationToken);

    Task AddSessionAsync(RankedSession session, CancellationToken cancellationToken);

    Task AddGuessAsync(RankedGuess guess, CancellationToken cancellationToken);

    Task UpdateSessionAsync(RankedSession session, CancellationToken cancellationToken);

    Task<RankedUserStats?> FindUserStatsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken);

    Task UpdateUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken);
}
