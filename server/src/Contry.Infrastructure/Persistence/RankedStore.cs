using Contry.Application.Ranked;
using Contry.Domain.Ranked;
using Microsoft.EntityFrameworkCore;

namespace Contry.Infrastructure.Persistence;

public sealed class RankedStore(ContryDbContext dbContext) : IRankedStore
{
    private readonly ContryDbContext _dbContext = dbContext;

    public Task<RankedChallenge?> FindChallengeByDateAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken)
        => _dbContext.RankedChallenges.SingleOrDefaultAsync(challenge => challenge.ChallengeDateUtc == challengeDateUtc, cancellationToken);

    public async Task AddChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken)
    {
        _dbContext.RankedChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RankedSession?> FindSessionByUserAndDateAsync(Guid userId, DateOnly challengeDateUtc, bool includeGuesses, CancellationToken cancellationToken)
    {
        var query = _dbContext.RankedSessions
            .Include(session => session.RankedChallenge)
            .Where(session => session.UserId == userId && session.RankedChallenge.ChallengeDateUtc == challengeDateUtc);

        if (includeGuesses)
        {
            query = query.Include(session => session.Guesses);
        }

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddSessionAsync(RankedSession session, CancellationToken cancellationToken)
    {
        _dbContext.RankedSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddGuessAsync(RankedGuess guess, CancellationToken cancellationToken)
    {
        _dbContext.RankedGuesses.Add(guess);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSessionAsync(RankedSession session, CancellationToken cancellationToken)
    {
        _dbContext.RankedSessions.Update(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RankedUserStats?> FindUserStatsAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.RankedUserStats.SingleOrDefaultAsync(stats => stats.UserId == userId, cancellationToken);

    public async Task AddUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken)
    {
        _dbContext.RankedUserStats.Add(stats);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken)
    {
        _dbContext.RankedUserStats.Update(stats);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
