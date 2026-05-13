using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked;

public interface IRankedDatasetProvider
{
    Task<RankedChallengeDefinition> GetChallengeDefinitionAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken);

    Task<RankedCountryRecord?> FindCountryAsync(string countryId, CancellationToken cancellationToken);
}
