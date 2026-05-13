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

    public async Task UpdateChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken)
    {
        _dbContext.RankedChallenges.Update(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSessionsByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var sessionIds = _dbContext.RankedSessions
            .Where(session => session.RankedChallenge.ChallengeDateUtc == date)
            .Select(session => session.Id);

        await _dbContext.RankedGuesses
            .Where(guess => sessionIds.Contains(guess.RankedSessionId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.RankedSessions
            .Where(session => session.RankedChallenge.ChallengeDateUtc == date)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ClearAllRankedDataAsync(CancellationToken cancellationToken)
    {
        await _dbContext.RankedClueUsageStats.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedCountryDiscoveryStats.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedUserStats.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedGuesses.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedSessions.ExecuteDeleteAsync(cancellationToken);
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

    public Task<RankedCountryDiscoveryStat?> FindCountryDiscoveryStatAsync(Guid userId, string countryId, CancellationToken cancellationToken)
        => _dbContext.RankedCountryDiscoveryStats.SingleOrDefaultAsync(stat => stat.UserId == userId && stat.CountryId == countryId, cancellationToken);

    public async Task AddCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken)
    {
        _dbContext.RankedCountryDiscoveryStats.Add(stat);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken)
    {
        _dbContext.RankedCountryDiscoveryStats.Update(stat);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RankedClueUsageStat?> FindClueUsageStatAsync(Guid userId, string clueId, CancellationToken cancellationToken)
        => _dbContext.RankedClueUsageStats.SingleOrDefaultAsync(stat => stat.UserId == userId && stat.ClueId == clueId, cancellationToken);

    public async Task AddClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken)
    {
        _dbContext.RankedClueUsageStats.Add(stat);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken)
    {
        _dbContext.RankedClueUsageStats.Update(stat);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<RankedUserStats> Items, int TotalCount)> GetLeaderboardStatsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.RankedUserStats
            .Include(stats => stats.User)
            .Where(stats => stats.WonCount >= 1) // Minimum 1 win
            .OrderBy(stats => (double)stats.TotalGuessesOnWins / stats.WonCount)
            .ThenBy(stats => stats.User.Username);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stats = await _dbContext.RankedUserStats.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.RankedUserStats.RemoveRange(stats);

        var discoveryStats = await _dbContext.RankedCountryDiscoveryStats.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.RankedCountryDiscoveryStats.RemoveRange(discoveryStats);

        var clueUsageStats = await _dbContext.RankedClueUsageStats.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.RankedClueUsageStats.RemoveRange(clueUsageStats);

        var sessions = await _dbContext.RankedSessions.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.RankedSessions.RemoveRange(sessions);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RankedCountryDiscoveryStat>> GetCountryDiscoveryStatsAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.RankedCountryDiscoveryStats.Where(stat => stat.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RankedClueUsageStat>> GetClueUsageStatsAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.RankedClueUsageStats.Where(stat => stat.UserId == userId).ToListAsync(cancellationToken);
}
