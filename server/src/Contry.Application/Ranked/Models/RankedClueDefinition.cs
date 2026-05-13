namespace Contry.Application.Ranked.Models;

public sealed record RankedClueDefinition(
    string Id,
    string Label,
    string Description,
    string Icon,
    RankedClueType Type,
    string Comparator,
    string? Group,
    string? UnitSymbol);
