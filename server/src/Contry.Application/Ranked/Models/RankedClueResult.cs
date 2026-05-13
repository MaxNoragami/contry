namespace Contry.Application.Ranked.Models;

public sealed record RankedClueResult(
    string ClueId,
    string Clue,
    string Value,
    RankedChipTone Tone,
    RankedClueKind Kind,
    RankedGuessTrend? Trend);
