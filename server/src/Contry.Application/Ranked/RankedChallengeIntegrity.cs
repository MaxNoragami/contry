using Contry.Application.Ranked.Models;
using Contry.Domain.Ranked;

namespace Contry.Application.Ranked;

public static class RankedChallengeIntegrity
{
    public static async Task<RankedChallenge?> ResetIfPublishedCluesMissingAsync(
        RankedChallenge? challenge,
        DateOnly challengeDateUtc,
        IRankedStore rankedStore,
        CancellationToken cancellationToken)
    {
        if (challenge is null)
        {
            return null;
        }

        var publishedCluePackIds = RankedChallengeSerialization.DeserializeClues(challenge.ClueSetJson)
            .Where(clue => clue.Source == RankedClueSource.Published && clue.RemoteId is not null)
            .Select(clue => clue.RemoteId!.Value)
            .Distinct()
            .ToArray();

        if (publishedCluePackIds.Length == 0)
        {
            return challenge;
        }

        var existingIds = await rankedStore.GetExistingCluePackIdsAsync(publishedCluePackIds, cancellationToken);
        if (publishedCluePackIds.All(existingIds.Contains))
        {
            return challenge;
        }

        await rankedStore.DeleteSessionsByDateAndRebuildStatsAsync(challengeDateUtc, cancellationToken);
        await rankedStore.DeleteChallengeByDateAsync(challengeDateUtc, cancellationToken);
        return null;
    }
}
