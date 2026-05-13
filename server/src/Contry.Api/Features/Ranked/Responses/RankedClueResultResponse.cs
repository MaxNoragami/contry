using Contry.Application.Ranked.Models;

namespace Contry.Api.Features.Ranked;

public sealed record RankedClueResultResponse(
    string ClueId,
    string Value,
    string Tone,
    string Kind,
    string? Trend)
{
    public static RankedClueResultResponse FromModel(RankedClueResult result)
        => new(
            result.ClueId,
            result.Value,
            result.Tone.ToString().ToLowerInvariant(),
            result.Kind.ToString().ToLowerInvariant(),
            result.Trend?.ToString().ToLowerInvariant());
}
