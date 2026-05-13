using Contry.Application.Ranked.Models;

namespace Contry.Api.Features.Ranked;

public sealed record RankedClueResponse(
    string Id,
    string Label,
    string Description,
    string Icon,
    string Type,
    string Comparator,
    string? Group,
    string? UnitSymbol)
{
    public static RankedClueResponse FromModel(RankedClueDefinition clue)
        => new(
            clue.Id,
            clue.Label,
            clue.Description,
            clue.Icon,
            ToTypeValue(clue.Type),
            clue.Comparator,
            clue.Group,
            clue.UnitSymbol);

    private static string ToTypeValue(RankedClueType type)
        => type switch
        {
            RankedClueType.Numeric => "numeric",
            RankedClueType.Categorical => "categorical",
            _ => "computed"
        };
}
