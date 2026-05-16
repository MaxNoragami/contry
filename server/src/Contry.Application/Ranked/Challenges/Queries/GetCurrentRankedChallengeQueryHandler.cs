using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked.Challenges.Queries;

public sealed class GetCurrentRankedChallengeQueryHandler(IRankedDatasetProvider rankedDatasetProvider, TimeProvider timeProvider)
{
    private readonly IRankedDatasetProvider _rankedDatasetProvider = rankedDatasetProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CurrentRankedChallengeResult> HandleAsync(GetCurrentRankedChallengeQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var challenge = await _rankedDatasetProvider.GetChallengeDefinitionAsync(today, cancellationToken);
        return new CurrentRankedChallengeResult(challenge.ChallengeDateUtc, challenge.Clues);
    }
}
