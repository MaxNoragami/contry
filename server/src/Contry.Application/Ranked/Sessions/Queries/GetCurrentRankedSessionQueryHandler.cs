using System.Text.Json;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked.Sessions.Queries;

public sealed class GetCurrentRankedSessionQueryHandler(IRankedStore rankedStore, IRankedDatasetProvider rankedDatasetProvider, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRankedStore _rankedStore = rankedStore;
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CurrentRankedSessionResult> HandleAsync(GetCurrentRankedSessionQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var session = await _rankedStore.FindSessionByUserAndDateAsync(query.UserId, today, includeGuesses: true, cancellationToken);

        if (session is null)
        {
            return new CurrentRankedSessionResult(today, "not_started", 0, null, []);
        }

        var guesses = session.Guesses
            .OrderBy(guess => guess.AttemptNumber)
            .Select(DeserializeGuess)
            .ToList();

        return new CurrentRankedSessionResult(
            today,
            ToApiStatus(session.Status),
            session.GuessCount,
            session.CompletedAtUtc,
            guesses);
    }

    private static RankedGuessRecordResult DeserializeGuess(Domain.Ranked.RankedGuess guess)
        => new(
            guess.AttemptNumber,
            guess.GuessCountryId,
            guess.GuessCountryName,
            JsonSerializer.Deserialize<List<RankedClueResult>>(guess.ResultsJson, JsonSerializerOptions) ?? [],
            guess.CreatedAtUtc);

    private static string ToApiStatus(Domain.Ranked.RankedSessionStatus status)
        => status switch
        {
            Domain.Ranked.RankedSessionStatus.Playing => "playing",
            Domain.Ranked.RankedSessionStatus.Won => "won",
            Domain.Ranked.RankedSessionStatus.Lost => "lost",
            _ => "playing"
        };
}
