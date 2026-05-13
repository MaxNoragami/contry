using System.Text.Json;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Domain.Ranked;

namespace Contry.Application.Ranked.Guesses.Commands;

public sealed class CreateRankedGuessCommandHandler(
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

    public async Task<CreateRankedGuessResult> HandleAsync(CreateRankedGuessCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var normalizedCountryId = command.CountryId.Trim().ToUpperInvariant();

        var challengeDefinition = await _rankedDatasetProvider.GetChallengeDefinitionAsync(today, cancellationToken);
        var guessedCountry = await _rankedDatasetProvider.FindCountryAsync(normalizedCountryId, cancellationToken)
            ?? throw new RankedInvalidCountryException();

        var challenge = await _rankedStore.FindChallengeByDateAsync(today, cancellationToken);
        if (challenge is null)
        {
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

        var targetCountry = await _rankedDatasetProvider.FindCountryAsync(challenge.TargetCountryId, cancellationToken)
            ?? throw new RankedInvalidCountryException();

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

        if (session.Guesses.Any(guess => string.Equals(guess.GuessCountryId, normalizedCountryId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new RankedDuplicateGuessException();
        }

        var clueSnapshot = DeserializeClues(challenge.ClueSetJson);
        var evaluatedResults = _rankedGuessEvaluator.Evaluate(guessedCountry, targetCountry, clueSnapshot, today);

        var guess = new RankedGuess
        {
            Id = Guid.NewGuid(),
            RankedSessionId = session.Id,
            AttemptNumber = session.GuessCount + 1,
            GuessCountryId = guessedCountry.CountryId,
            GuessCountryName = guessedCountry.Name,
            ResultsJson = JsonSerializer.Serialize(evaluatedResults, JsonSerializerOptions),
            CreatedAtUtc = now
        };

        await _rankedStore.AddGuessAsync(guess, cancellationToken);
        session.GuessCount = guess.AttemptNumber;

        if (string.Equals(guessedCountry.CountryId, targetCountry.CountryId, StringComparison.Ordinal))
        {
            session.Status = RankedSessionStatus.Won;
            session.CompletedAtUtc = now;
            await UpdateStatsAsync(command.UserId, today, session.GuessCount, targetCountry.CountryId, clueSnapshot, cancellationToken);
        }

        await _rankedStore.UpdateSessionAsync(session, cancellationToken);

        var guesses = session.Guesses
            .OrderBy(item => item.AttemptNumber)
            .Select(DeserializeGuess)
            .ToList();

        return new CreateRankedGuessResult(
            today,
            session.Status == RankedSessionStatus.Won ? "won" : "playing",
            session.GuessCount,
            session.CompletedAtUtc,
            DeserializeGuess(guess));
    }

    private async Task UpdateStatsAsync(
        Guid userId,
        DateOnly challengeDateUtc,
        int guessCount,
        string targetCountryId,
        IReadOnlyList<RankedClueDefinition> clues,
        CancellationToken cancellationToken)
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
        stats.WonCount += 1;
        stats.TotalGuessesOnWins += guessCount;
        stats.FastestWinGuessCount = stats.FastestWinGuessCount is null ? guessCount : Math.Min(stats.FastestWinGuessCount.Value, guessCount);
        stats.SlowestWinGuessCount = stats.SlowestWinGuessCount is null ? guessCount : Math.Max(stats.SlowestWinGuessCount.Value, guessCount);

        var extendsStreak = stats.LastCompletedChallengeDateUtc is not null
            && stats.LastCompletedChallengeDateUtc.Value.AddDays(1) == challengeDateUtc;
        stats.CurrentStreak = extendsStreak ? stats.CurrentStreak + 1 : 1;
        stats.BestStreak = Math.Max(stats.BestStreak, stats.CurrentStreak);
        stats.LastCompletedChallengeDateUtc = challengeDateUtc;

        var distribution = JsonSerializer.Deserialize<Dictionary<string, int>>(stats.GuessDistributionJson, JsonSerializerOptions) ?? [];
        var guessCountStr = guessCount.ToString();
        distribution[guessCountStr] = distribution.TryGetValue(guessCountStr, out var count) ? count + 1 : 1;
        stats.GuessDistributionJson = JsonSerializer.Serialize(distribution, JsonSerializerOptions);

        if (isNewStats)
        {
            await _rankedStore.AddUserStatsAsync(stats, cancellationToken);
        }
        else
        {
            await _rankedStore.UpdateUserStatsAsync(stats, cancellationToken);
        }

        var discoveryStat = await _rankedStore.FindCountryDiscoveryStatAsync(userId, targetCountryId, cancellationToken);
        var isNewDiscovery = false;
        if (discoveryStat is null)
        {
            discoveryStat = new RankedCountryDiscoveryStat
            {
                UserId = userId,
                CountryId = targetCountryId,
                Discovered = true,
                SolvedCount = 0
            };
            isNewDiscovery = true;
        }

        discoveryStat.SolvedCount += 1;
        discoveryStat.LastSolvedAtUtc = _timeProvider.GetUtcNow();
        discoveryStat.BestAttempts = discoveryStat.BestAttempts is null ? guessCount : Math.Min(discoveryStat.BestAttempts.Value, guessCount);

        if (isNewDiscovery)
        {
            await _rankedStore.AddCountryDiscoveryStatAsync(discoveryStat, cancellationToken);
        }
        else
        {
            await _rankedStore.UpdateCountryDiscoveryStatAsync(discoveryStat, cancellationToken);
        }

        foreach (var clue in clues)
        {
            var clueStat = await _rankedStore.FindClueUsageStatAsync(userId, clue.Id, cancellationToken);
            if (clueStat is null)
            {
                clueStat = new RankedClueUsageStat
                {
                    UserId = userId,
                    ClueId = clue.Id,
                    UsageCount = 1
                };
                await _rankedStore.AddClueUsageStatAsync(clueStat, cancellationToken);
            }
            else
            {
                clueStat.UsageCount += 1;
                await _rankedStore.UpdateClueUsageStatAsync(clueStat, cancellationToken);
            }
        }
    }

    private static IReadOnlyList<RankedClueDefinition> DeserializeClues(string json)
        => JsonSerializer.Deserialize<List<RankedClueDefinition>>(json, JsonSerializerOptions) ?? [];

    private static RankedGuessRecordResult DeserializeGuess(RankedGuess guess)
        => new(
            guess.AttemptNumber,
            guess.GuessCountryId,
            guess.GuessCountryName,
            JsonSerializer.Deserialize<List<RankedClueResult>>(guess.ResultsJson, JsonSerializerOptions) ?? [],
            guess.CreatedAtUtc);
}
