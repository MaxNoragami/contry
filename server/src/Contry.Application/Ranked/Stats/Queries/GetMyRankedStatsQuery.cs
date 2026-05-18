using Contry.Application.Ranked;

namespace Contry.Application.Ranked.Stats.Queries;

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

public sealed record ClueUsageStatDto(string ClueId, int UsageCount, string Label, string? Icon);

public sealed class GetMyRankedStatsQueryHandler(IRankedStore rankedStore, IRankedDatasetProvider rankedDatasetProvider)
{
    private readonly IRankedStore _rankedStore = rankedStore;
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;

    public async Task<MyRankedStatsResult> HandleAsync(GetMyRankedStatsQuery query, CancellationToken cancellationToken)
    {
        var stats = await _rankedStore.FindUserStatsAsync(query.UserId, cancellationToken);
        var discoveryStats = await _rankedStore.GetCountryDiscoveryStatsAsync(query.UserId, cancellationToken);
        var clueStats = await _rankedStore.GetClueUsageStatsAsync(query.UserId, cancellationToken);
        var builtinClues = await _rankedDatasetProvider.GetBuiltinClueCatalogAsync(cancellationToken);
        var builtinById = builtinClues.ToDictionary(clue => clue.Id, clue => clue, StringComparer.Ordinal);
        var missingIds = clueStats.Select(stat => stat.ClueId).Where(id => !builtinById.ContainsKey(id)).Distinct(StringComparer.Ordinal).ToArray();
        var publishedById = await _rankedStore.GetPublishedCluePackStatMetadataByDatasetIdsAsync(missingIds, cancellationToken);

        var discoveryDtos = discoveryStats.Select(d => new CountryDiscoveryStatDto(
            d.CountryId,
            d.Discovered,
            d.BestAttempts,
            d.SolvedCount,
            d.LastSolvedAtUtc)).ToList();

        var clueDtos = clueStats.Select(c =>
        {
            if (builtinById.TryGetValue(c.ClueId, out var builtin))
            {
                return new ClueUsageStatDto(c.ClueId, c.UsageCount, builtin.Label, builtin.Icon);
            }

            if (publishedById.TryGetValue(c.ClueId, out var published))
            {
                return new ClueUsageStatDto(c.ClueId, c.UsageCount, published.Label, published.Icon);
            }

            return new ClueUsageStatDto(c.ClueId, c.UsageCount, c.ClueId, null);
        }).ToList();

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
