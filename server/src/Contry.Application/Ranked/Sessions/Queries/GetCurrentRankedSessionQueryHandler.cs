using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Application.Ranked.Sessions;

namespace Contry.Application.Ranked.Sessions.Queries;

public sealed class GetCurrentRankedSessionQueryHandler(
    IRankedStore rankedStore,
    IRankedDatasetProvider rankedDatasetProvider,
    RankedGuessEvaluator rankedGuessEvaluator,
    TimeProvider timeProvider)
{
    private readonly IRankedStore _rankedStore = rankedStore;
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;
    private readonly RankedGuessEvaluator _rankedGuessEvaluator = rankedGuessEvaluator;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CurrentRankedSessionResult> HandleAsync(GetCurrentRankedSessionQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var challenge = await _rankedStore.FindChallengeByDateAsync(today, cancellationToken);
        challenge = await RankedChallengeIntegrity.ResetIfPublishedCluesMissingAsync(challenge, today, _rankedStore, cancellationToken);
        var session = await _rankedStore.FindSessionByUserAndDateAsync(query.UserId, today, includeGuesses: true, cancellationToken);

        if (session is null)
        {
            return RankedSessionResultFactory.CreateNotStarted(today);
        }

        return await RankedSessionResultFactory.BuildAsync(session, _rankedDatasetProvider, _rankedGuessEvaluator, cancellationToken);
    }
}
