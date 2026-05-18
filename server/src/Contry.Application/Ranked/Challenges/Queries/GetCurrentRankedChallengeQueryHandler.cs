using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked.Challenges.Queries;

public sealed class GetCurrentRankedChallengeQueryHandler(IRankedDatasetProvider rankedDatasetProvider, IRankedStore rankedStore, TimeProvider timeProvider)
{
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;
    private readonly IRankedStore _rankedStore = rankedStore;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CurrentRankedChallengeResult> HandleAsync(GetCurrentRankedChallengeQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var persisted = await _rankedStore.FindChallengeByDateAsync(today, cancellationToken);
        persisted = await RankedChallengeIntegrity.ResetIfPublishedCluesMissingAsync(persisted, today, _rankedStore, cancellationToken);
        if (persisted is not null)
        {
            return new CurrentRankedChallengeResult(today, RankedChallengeSerialization.DeserializeClues(persisted.ClueSetJson));
        }

        var challenge = await _rankedDatasetProvider.GetChallengeDefinitionAsync(today, cancellationToken);
        return new CurrentRankedChallengeResult(challenge.ChallengeDateUtc, challenge.Clues);
    }
}
