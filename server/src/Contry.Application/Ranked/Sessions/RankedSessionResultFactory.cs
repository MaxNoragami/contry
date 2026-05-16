using System.Text.Json;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Domain.Ranked;

namespace Contry.Application.Ranked.Sessions;

internal static class RankedSessionResultFactory
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static CurrentRankedSessionResult CreateNotStarted(DateOnly challengeDateUtc)
        => new(challengeDateUtc, "not_started", 0, null, []);

    public static async Task<CurrentRankedSessionResult> BuildAsync(
        RankedSession session,
        IRankedDatasetProvider rankedDatasetProvider,
        RankedGuessEvaluator rankedGuessEvaluator,
        CancellationToken cancellationToken)
    {
        var guesses = session.Guesses
            .OrderBy(guess => guess.AttemptNumber)
            .Select(DeserializeGuess)
            .ToList();

        if (session.Status == RankedSessionStatus.Lost)
        {
            var clues = DeserializeClues(session.RankedChallenge.ClueSetJson);
            var targetCountry = await rankedDatasetProvider.FindCountryAsync(session.RankedChallenge.TargetCountryId, cancellationToken)
                ?? throw new RankedInvalidCountryException();

            guesses.Add(new RankedGuessRecordResult(
                session.GuessCount + 1,
                targetCountry.CountryId,
                targetCountry.Name,
                rankedGuessEvaluator.RevealTarget(targetCountry, clues, session.RankedChallenge.ChallengeDateUtc),
                session.CompletedAtUtc ?? session.StartedAtUtc));
        }

        return new CurrentRankedSessionResult(
            session.RankedChallenge.ChallengeDateUtc,
            ToApiStatus(session.Status),
            session.GuessCount,
            session.CompletedAtUtc,
            guesses);
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

    private static string ToApiStatus(RankedSessionStatus status)
        => status switch
        {
            RankedSessionStatus.Playing => "playing",
            RankedSessionStatus.Won => "won",
            RankedSessionStatus.Lost => "lost",
            _ => "playing"
        };
}
