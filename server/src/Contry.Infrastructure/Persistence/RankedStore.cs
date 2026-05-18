using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Domain.Clues;
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

    public async Task DeleteChallengeByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await _dbContext.RankedChallenges
            .Where(challenge => challenge.ChallengeDateUtc == date)
            .ExecuteDeleteAsync(cancellationToken);
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

    public async Task DeleteSessionsByDateAndRebuildStatsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await DeleteSessionsByDateAsync(date, cancellationToken);
        await RebuildAggregatesAsync(cancellationToken);
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

    public async Task<IReadOnlySet<Guid>> GetExistingCluePackIdsAsync(IReadOnlyCollection<Guid> cluePackIds, CancellationToken cancellationToken)
    {
        if (cluePackIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await _dbContext.CluePacks
            .Where(pack => cluePackIds.Contains(pack.Id))
            .Select(pack => pack.Id)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<string, PublishedCluePackStatMetadata>> GetPublishedCluePackStatMetadataByDatasetIdsAsync(IReadOnlyCollection<string> datasetIds, CancellationToken cancellationToken)
    {
        if (datasetIds.Count == 0)
        {
            return new Dictionary<string, PublishedCluePackStatMetadata>(StringComparer.Ordinal);
        }

        var items = await _dbContext.CluePacks
            .Where(pack => datasetIds.Contains(pack.DatasetId))
            .Select(pack => new PublishedCluePackStatMetadata(pack.DatasetId, pack.Label, pack.Icon))
            .ToListAsync(cancellationToken);

        return items.ToDictionary(item => item.DatasetId, item => item, StringComparer.Ordinal);
    }

    private async Task RebuildAggregatesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.RankedClueUsageStats.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedCountryDiscoveryStats.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RankedUserStats.ExecuteDeleteAsync(cancellationToken);

        var sessions = await _dbContext.RankedSessions
            .Include(session => session.RankedChallenge)
            .Include(session => session.Guesses)
            .Where(session => session.Status == RankedSessionStatus.Won || session.Status == RankedSessionStatus.Lost)
            .OrderBy(session => session.UserId)
            .ThenBy(session => session.RankedChallenge.ChallengeDateUtc)
            .ThenBy(session => session.CompletedAtUtc)
            .ToListAsync(cancellationToken);

        var statsByUser = new Dictionary<Guid, RankedUserStats>();
        var clueUsage = new Dictionary<(Guid UserId, string ClueId), RankedClueUsageStat>();
        var discovery = new Dictionary<(Guid UserId, string CountryId), RankedCountryDiscoveryStat>();

        foreach (var userSessions in sessions.GroupBy(session => session.UserId))
        {
            var distribution = new Dictionary<string, int>(StringComparer.Ordinal);
            var currentStreak = 0;
            var bestStreak = 0;
            DateOnly? lastWonDate = null;
            DateOnly? lastCompletedDate = null;
            var stats = new RankedUserStats { UserId = userSessions.Key };

            foreach (var session in userSessions)
            {
                stats.PlayedCount += 1;
                lastCompletedDate = session.RankedChallenge.ChallengeDateUtc;

                if (session.Status == RankedSessionStatus.Won)
                {
                    stats.WonCount += 1;
                    stats.TotalGuessesOnWins += session.GuessCount;
                    stats.FastestWinGuessCount = stats.FastestWinGuessCount is null ? session.GuessCount : Math.Min(stats.FastestWinGuessCount.Value, session.GuessCount);
                    stats.SlowestWinGuessCount = stats.SlowestWinGuessCount is null ? session.GuessCount : Math.Max(stats.SlowestWinGuessCount.Value, session.GuessCount);

                    var bucket = session.GuessCount.ToString();
                    distribution[bucket] = distribution.TryGetValue(bucket, out var count) ? count + 1 : 1;

                    currentStreak = lastWonDate is not null && lastWonDate.Value.AddDays(1) == session.RankedChallenge.ChallengeDateUtc
                        ? currentStreak + 1
                        : 1;
                    bestStreak = Math.Max(bestStreak, currentStreak);
                    lastWonDate = session.RankedChallenge.ChallengeDateUtc;

                    var discoveryKey = (session.UserId, session.RankedChallenge.TargetCountryId);
                    if (!discovery.TryGetValue(discoveryKey, out var discoveryStat))
                    {
                        discoveryStat = new RankedCountryDiscoveryStat
                        {
                            UserId = session.UserId,
                            CountryId = session.RankedChallenge.TargetCountryId,
                            Discovered = true,
                            SolvedCount = 0
                        };
                        discovery[discoveryKey] = discoveryStat;
                    }

                    discoveryStat.SolvedCount += 1;
                    discoveryStat.BestAttempts = discoveryStat.BestAttempts is null ? session.GuessCount : Math.Min(discoveryStat.BestAttempts.Value, session.GuessCount);
                    discoveryStat.LastSolvedAtUtc = session.CompletedAtUtc;

                    foreach (var clue in RankedChallengeSerialization.DeserializeClues(session.RankedChallenge.ClueSetJson))
                    {
                        var clueKey = (session.UserId, clue.Id);
                        if (!clueUsage.TryGetValue(clueKey, out var clueStat))
                        {
                            clueStat = new RankedClueUsageStat
                            {
                                UserId = session.UserId,
                                ClueId = clue.Id,
                                UsageCount = 0,
                            };
                            clueUsage[clueKey] = clueStat;
                        }

                        clueStat.UsageCount += 1;
                    }
                }
                else
                {
                    distribution["DNF"] = distribution.TryGetValue("DNF", out var count) ? count + 1 : 1;
                    currentStreak = 0;
                }
            }

            stats.CurrentStreak = currentStreak;
            stats.BestStreak = bestStreak;
            stats.LastCompletedChallengeDateUtc = lastCompletedDate;
            stats.GuessDistributionJson = System.Text.Json.JsonSerializer.Serialize(distribution);
            statsByUser[userSessions.Key] = stats;
        }

        if (statsByUser.Count > 0)
        {
            _dbContext.RankedUserStats.AddRange(statsByUser.Values);
        }

        if (discovery.Count > 0)
        {
            _dbContext.RankedCountryDiscoveryStats.AddRange(discovery.Values);
        }

        if (clueUsage.Count > 0)
        {
            _dbContext.RankedClueUsageStats.AddRange(clueUsage.Values);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
