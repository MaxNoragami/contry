using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Domain.Ranked;

namespace Contry.Application.Ranked.Sessions;

internal static class RankedSessionResultFactory
{
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
            var clues = RankedChallengeSerialization.DeserializeClues(session.RankedChallenge.ClueSetJson);
            var customClueData = RankedChallengeSerialization.DeserializeCustomClueData(session.RankedChallenge.CustomClueDataJson);
            var targetCountry = await rankedDatasetProvider.FindCountryAsync(session.RankedChallenge.TargetCountryId, cancellationToken)
                ?? throw new RankedInvalidCountryException();
            targetCountry = RankedChallengeSerialization.ApplyCustomClueData(targetCountry, customClueData);

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
    private static RankedGuessRecordResult DeserializeGuess(RankedGuess guess)
        => new(
            guess.AttemptNumber,
            guess.GuessCountryId,
            guess.GuessCountryName,
            System.Text.Json.JsonSerializer.Deserialize<List<RankedClueResult>>(guess.ResultsJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [],
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
