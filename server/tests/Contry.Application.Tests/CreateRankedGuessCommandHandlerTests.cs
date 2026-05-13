using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Domain.Ranked;

namespace Contry.Application.Tests;

public sealed class CreateRankedGuessCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesSessionAndGuess()
    {
        var store = new FakeRankedStore();
        var provider = new FakeRankedDatasetProvider();
        var handler = new CreateRankedGuessCommandHandler(store, provider, new RankedGuessEvaluator(), new FakeTimeProvider(new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new CreateRankedGuessCommand(Guid.NewGuid(), "RO"), CancellationToken.None);

        Assert.Equal("playing", result.Status);
        Assert.Equal(1, result.GuessCount);
        Assert.Equal("RO", result.Guess.GuessCountryId);
        Assert.Single(store.Challenges);
        Assert.Single(store.Sessions);
    }

    [Fact]
    public async Task HandleAsync_DuplicateGuess_ThrowsConflict()
    {
        var userId = Guid.NewGuid();
        var store = new FakeRankedStore();
        var provider = new FakeRankedDatasetProvider();
        var handler = new CreateRankedGuessCommandHandler(store, provider, new RankedGuessEvaluator(), new FakeTimeProvider(new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero)));

        await handler.HandleAsync(new CreateRankedGuessCommand(userId, "RO"), CancellationToken.None);

        await Assert.ThrowsAsync<RankedDuplicateGuessException>(() => handler.HandleAsync(new CreateRankedGuessCommand(userId, "RO"), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_CorrectGuess_CompletesSessionAndUpdatesStats()
    {
        var userId = Guid.NewGuid();
        var store = new FakeRankedStore();
        var provider = new FakeRankedDatasetProvider();
        var handler = new CreateRankedGuessCommandHandler(store, provider, new RankedGuessEvaluator(), new FakeTimeProvider(new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new CreateRankedGuessCommand(userId, "MD"), CancellationToken.None);

        Assert.Equal("won", result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal("MD", result.Guess.GuessCountryId);
        Assert.Single(store.UserStats);
        Assert.Equal(1, store.UserStats[userId].WonCount);
        Assert.Equal(1, store.UserStats[userId].PlayedCount);
    }

    [Fact]
    public async Task HandleAsync_UsesPersistedChallengeTarget_WhenChallengeAlreadyExists()
    {
        var today = new DateOnly(2026, 5, 13);
        var store = new FakeRankedStore();
        store.Challenges.Add(new RankedChallenge
        {
            Id = Guid.NewGuid(),
            ChallengeDateUtc = today,
            TargetCountryId = "RO",
            ClueSetJson = "[]",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero)
        });

        var provider = new FakeRankedDatasetProvider();
        var handler = new CreateRankedGuessCommandHandler(store, provider, new RankedGuessEvaluator(), new FakeTimeProvider(new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new CreateRankedGuessCommand(Guid.NewGuid(), "RO"), CancellationToken.None);

        Assert.Equal("won", result.Status);
        Assert.Equal("RO", result.Guess.GuessCountryId);
    }

    private sealed class FakeRankedStore : IRankedStore
    {
        public List<RankedChallenge> Challenges { get; } = [];
        public List<RankedSession> Sessions { get; } = [];
        public Dictionary<Guid, RankedUserStats> UserStats { get; } = [];

        public Task<RankedChallenge?> FindChallengeByDateAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken)
            => Task.FromResult(Challenges.SingleOrDefault(challenge => challenge.ChallengeDateUtc == challengeDateUtc));

        public Task AddChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken)
        {
            Challenges.Add(challenge);
            return Task.CompletedTask;
        }

        public Task UpdateChallengeAsync(RankedChallenge challenge, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteSessionsByDateAsync(DateOnly date, CancellationToken cancellationToken)
        {
            Sessions.RemoveAll(session => session.RankedChallenge.ChallengeDateUtc == date);
            return Task.CompletedTask;
        }

        public Task ClearAllRankedDataAsync(CancellationToken cancellationToken)
        {
            Sessions.Clear();
            UserStats.Clear();
            return Task.CompletedTask;
        }

        public Task<RankedSession?> FindSessionByUserAndDateAsync(Guid userId, DateOnly challengeDateUtc, bool includeGuesses, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.SingleOrDefault(session => session.UserId == userId && session.RankedChallenge.ChallengeDateUtc == challengeDateUtc));

        public Task AddSessionAsync(RankedSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task AddGuessAsync(RankedGuess guess, CancellationToken cancellationToken)
        {
            var session = Sessions.Single(item => item.Id == guess.RankedSessionId);
            session.Guesses.Add(guess);
            return Task.CompletedTask;
        }


        public Task UpdateSessionAsync(RankedSession session, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<RankedUserStats?> FindUserStatsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(UserStats.TryGetValue(userId, out var stats) ? stats : null);

        public Task AddUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken)
        {
            UserStats[stats.UserId] = stats;
            return Task.CompletedTask;
        }

        public Task UpdateUserStatsAsync(RankedUserStats stats, CancellationToken cancellationToken)
        {
            UserStats[stats.UserId] = stats;
            return Task.CompletedTask;
        }

        public Task<RankedCountryDiscoveryStat?> FindCountryDiscoveryStatAsync(Guid userId, string countryId, CancellationToken cancellationToken)
            => Task.FromResult<RankedCountryDiscoveryStat?>(null);

        public Task AddCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateCountryDiscoveryStatAsync(RankedCountryDiscoveryStat stat, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<RankedClueUsageStat?> FindClueUsageStatAsync(Guid userId, string clueId, CancellationToken cancellationToken)
            => Task.FromResult<RankedClueUsageStat?>(null);

        public Task AddClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateClueUsageStatAsync(RankedClueUsageStat stat, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<RankedUserStats> Items, int TotalCount)> GetLeaderboardStatsAsync(int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(((IReadOnlyList<RankedUserStats>)[], 0));

        public Task DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<RankedCountryDiscoveryStat>> GetCountryDiscoveryStatsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RankedCountryDiscoveryStat>>([]);

        public Task<IReadOnlyList<RankedClueUsageStat>> GetClueUsageStatsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RankedClueUsageStat>>([]);
    }

    private sealed class FakeRankedDatasetProvider : IRankedDatasetProvider
    {
        private static readonly IReadOnlyList<RankedClueDefinition> Clues =
        [
            new("hemisphere", "Hemisphere", "", "globe", RankedClueType.Computed, "exact", null, null),
            new("continent", "Continent", "", "compass", RankedClueType.Categorical, "exact", null, null),
            new("temperature_avg_c", "Average Temperature", "", "thermometer", RankedClueType.Numeric, "higher_lower", "temperature_avg_c", "degC"),
            new("population", "Population", "", "users", RankedClueType.Numeric, "higher_lower", null, null),
            new("coordinates", "Coordinates", "", "navigation", RankedClueType.Computed, "coordinates", null, null)
        ];

        private static readonly Dictionary<string, RankedCountryRecord> Countries = new(StringComparer.Ordinal)
        {
            ["MD"] = new RankedCountryRecord("MD", "Moldova", 47d, 28d, new Dictionary<string, string?>
            {
                ["continent"] = "Europe",
                ["population"] = "100",
                ["temperature_avg_c_m05"] = "12.5"
            }),
            ["RO"] = new RankedCountryRecord("RO", "Romania", 45d, 25d, new Dictionary<string, string?>
            {
                ["continent"] = "Europe",
                ["population"] = "200",
                ["temperature_avg_c_m05"] = "15.0"
            })
        };

        public Task<RankedChallengeDefinition> GetChallengeDefinitionAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken)
            => Task.FromResult(new RankedChallengeDefinition(challengeDateUtc, "MD", Clues));

        public Task<RankedCountryRecord?> FindCountryAsync(string countryId, CancellationToken cancellationToken)
            => Task.FromResult(Countries.TryGetValue(countryId, out var country) ? country : null);
    }
}
