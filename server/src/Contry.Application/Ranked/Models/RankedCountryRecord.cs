namespace Contry.Application.Ranked.Models;

public sealed record RankedCountryRecord(
    string CountryId,
    string Name,
    double Lat,
    double Lon,
    IReadOnlyDictionary<string, string?> Values);
