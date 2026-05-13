namespace Contry.Application.Ranked;

public sealed record GetMyRankedStatsQuery(Guid UserId);

public sealed record MyRankedStatsResult(
    int PlayedCount,
    int WonCount,
    int TotalGuessesOnWins,
    int? FastestWinGuessCount,
    int? SlowestWinGuessCount,
    int CurrentStreak,
    int BestStreak,
    string GuessDistributionJson,
    IReadOnlyList<CountryDiscoveryStatDto> CountryDiscoveryStats,
    IReadOnlyList<ClueUsageStatDto> ClueUsageStats);

public sealed record CountryDiscoveryStatDto(string CountryId, bool Discovered, int? BestAttempts, int SolvedCount, DateTimeOffset? LastSolvedAtUtc);

public sealed record ClueUsageStatDto(string ClueId, int UsageCount);

public sealed class GetMyRankedStatsQueryHandler(IRankedStore rankedStore)
{
    private readonly IRankedStore _rankedStore = rankedStore;

    public async Task<MyRankedStatsResult> HandleAsync(GetMyRankedStatsQuery query, CancellationToken cancellationToken)
    {
        var stats = await _rankedStore.FindUserStatsAsync(query.UserId, cancellationToken);
        var discoveryStats = await _rankedStore.GetCountryDiscoveryStatsAsync(query.UserId, cancellationToken);
        var clueStats = await _rankedStore.GetClueUsageStatsAsync(query.UserId, cancellationToken);

        var discoveryDtos = discoveryStats.Select(d => new CountryDiscoveryStatDto(
            d.CountryId,
            d.Discovered,
            d.BestAttempts,
            d.SolvedCount,
            d.LastSolvedAtUtc)).ToList();

        var clueDtos = clueStats.Select(c => new ClueUsageStatDto(
            c.ClueId,
            c.UsageCount)).ToList();

        if (stats is null)
        {
            return new MyRankedStatsResult(0, 0, 0, null, null, 0, 0, "{}", discoveryDtos, clueDtos);
        }

        return new MyRankedStatsResult(
            stats.PlayedCount,
            stats.WonCount,
            stats.TotalGuessesOnWins,
            stats.FastestWinGuessCount,
            stats.SlowestWinGuessCount,
            stats.CurrentStreak,
            stats.BestStreak,
            stats.GuessDistributionJson,
            discoveryDtos,
            clueDtos);
    }
}
