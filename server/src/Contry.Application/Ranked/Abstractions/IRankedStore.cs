using Contry.Domain.Ranked;

namespace Contry.Application.Ranked;

public sealed record PublishedCluePackStatMetadata(string DatasetId, string Label, string Icon);

public interface IRankedStore
{
    Task<RankedChallenge?> FindChallengeByDateAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken);

    Task AddChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken);

    Task UpdateChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken);

    Task DeleteChallengeByDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task DeleteSessionsByDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task DeleteSessionsByDateAndRebuildStatsAsync(DateOnly date, CancellationToken cancellationToken);

    Task ClearAllRankedDataAsync(CancellationToken cancellationToken);

    Task<RankedSession?> FindSessionByUserAndDateAsync(Guid userId, DateOnly challengeDateUtc, bool includeGuesses, CancellationToken cancellationToken);

    Task AddSessionAsync(RankedSession session, CancellationToken cancellationToken);

    Task AddGuessAsync(RankedGuess guess, CancellationToken cancellationToken);

    Task UpdateSessionAsync(RankedSession session, CancellationToken cancellationToken);

    Task<RankedUserStats?> FindUserStatsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken);

    Task UpdateUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken);

    Task<RankedCountryDiscoveryStat?> FindCountryDiscoveryStatAsync(Guid userId, string countryId, CancellationToken cancellationToken);

    Task AddCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken);

    Task UpdateCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken);

    Task<RankedClueUsageStat?> FindClueUsageStatAsync(Guid userId, string clueId, CancellationToken cancellationToken);

    Task AddClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken);

    Task UpdateClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken);

    Task<(IReadOnlyList<RankedUserStats> Items, int TotalCount)> GetLeaderboardStatsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RankedCountryDiscoveryStat>> GetCountryDiscoveryStatsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RankedClueUsageStat>> GetClueUsageStatsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetExistingCluePackIdsAsync(IReadOnlyCollection<Guid> cluePackIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, PublishedCluePackStatMetadata>> GetPublishedCluePackStatMetadataByDatasetIdsAsync(IReadOnlyCollection<string> datasetIds, CancellationToken cancellationToken);
}
