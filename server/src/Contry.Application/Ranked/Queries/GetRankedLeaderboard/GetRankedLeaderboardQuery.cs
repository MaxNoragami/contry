namespace Contry.Application.Ranked;

public sealed record GetRankedLeaderboardQuery(int Page, int PageSize);

public sealed record RankedLeaderboardEntry(string Username, double AverageTries, int PlayedCount);

public sealed record GetRankedLeaderboardResult(IReadOnlyList<RankedLeaderboardEntry> Items, int TotalCount, int Page, int PageSize);

public sealed class GetRankedLeaderboardQueryHandler(IRankedStore rankedStore)
{
    private readonly IRankedStore _rankedStore = rankedStore;

    public async Task<GetRankedLeaderboardResult> HandleAsync(GetRankedLeaderboardQuery query, CancellationToken cancellationToken)
    {
        var (stats, totalCount) = await _rankedStore.GetLeaderboardStatsAsync(query.Page, query.PageSize, cancellationToken);

        var items = stats.Select(s => new RankedLeaderboardEntry(
            s.User.Username,
            (double)s.TotalGuessesOnWins / s.WonCount,
            s.PlayedCount)).ToList();

        return new GetRankedLeaderboardResult(items, totalCount, query.Page, query.PageSize);
    }
}
