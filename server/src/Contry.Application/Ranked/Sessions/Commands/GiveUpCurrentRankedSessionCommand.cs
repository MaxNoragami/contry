using System.Text.Json;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Application.Ranked.Sessions;
using Contry.Domain.Ranked;

namespace Contry.Application.Ranked.Sessions.Commands;

public sealed record GiveUpCurrentRankedSessionCommand(Guid UserId);

public sealed class GiveUpCurrentRankedSessionCommandHandler(
    IRankedStore rankedStore,
    IRankedDatasetProvider rankedDatasetProvider,
    RankedGuessEvaluator rankedGuessEvaluator,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRankedStore _rankedStore = rankedStore;
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;
    private readonly RankedGuessEvaluator _rankedGuessEvaluator = rankedGuessEvaluator;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CurrentRankedSessionResult> HandleAsync(GiveUpCurrentRankedSessionCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var challenge = await _rankedStore.FindChallengeByDateAsync(today, cancellationToken);
        if (challenge is null)
        {
            var challengeDefinition = await _rankedDatasetProvider.GetChallengeDefinitionAsync(today, cancellationToken);
            challenge = new RankedChallenge
            {
                Id = Guid.NewGuid(),
                ChallengeDateUtc = today,
                TargetCountryId = challengeDefinition.TargetCountryId,
                ClueSetJson = JsonSerializer.Serialize(challengeDefinition.Clues, JsonSerializerOptions),
                CreatedAtUtc = now
            };

            await _rankedStore.AddChallengeAsync(challenge, cancellationToken);
        }

        var session = await _rankedStore.FindSessionByUserAndDateAsync(command.UserId, today, includeGuesses: true, cancellationToken);
        if (session is null)
        {
            session = new RankedSession
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                RankedChallengeId = challenge.Id,
                RankedChallenge = challenge,
                StartedAtUtc = now,
                Status = RankedSessionStatus.Playing,
                GuessCount = 0
            };

            await _rankedStore.AddSessionAsync(session, cancellationToken);
        }

        if (session.IsCompleted)
        {
            throw new RankedSessionCompletedException();
        }

        session.Status = RankedSessionStatus.Lost;
        session.CompletedAtUtc = now;

        await UpdateStatsAsync(command.UserId, today, cancellationToken);
        await _rankedStore.UpdateSessionAsync(session, cancellationToken);

        return await RankedSessionResultFactory.BuildAsync(session, _rankedDatasetProvider, _rankedGuessEvaluator, cancellationToken);
    }

    private async Task UpdateStatsAsync(Guid userId, DateOnly challengeDateUtc, CancellationToken cancellationToken)
    {
        var stats = await _rankedStore.FindUserStatsAsync(userId, cancellationToken);
        var isNewStats = false;

        if (stats is null)
        {
            stats = new RankedUserStats
            {
                UserId = userId
            };
            isNewStats = true;
        }

        stats.PlayedCount += 1;
        stats.CurrentStreak = 0;
        stats.LastCompletedChallengeDateUtc = challengeDateUtc;

        var distribution = JsonSerializer.Deserialize<Dictionary<string, int>>(stats.GuessDistributionJson, JsonSerializerOptions) ?? [];
        distribution["DNF"] = distribution.TryGetValue("DNF", out var count) ? count + 1 : 1;
        stats.GuessDistributionJson = JsonSerializer.Serialize(distribution, JsonSerializerOptions);

        if (isNewStats)
        {
            await _rankedStore.AddUserStatsAsync(stats, cancellationToken);
        }
        else
        {
            await _rankedStore.UpdateUserStatsAsync(stats, cancellationToken);
        }
    }
}
