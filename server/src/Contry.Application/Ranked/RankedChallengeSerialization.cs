using System.Text.Json;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked;

public static class RankedChallengeSerialization
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<RankedClueDefinition> DeserializeClues(string json)
        => JsonSerializer.Deserialize<List<RankedClueDefinition>>(json, JsonSerializerOptions) ?? [];

    public static string SerializeClues(IReadOnlyList<RankedClueDefinition> clues)
        => JsonSerializer.Serialize(clues, JsonSerializerOptions);

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> DeserializeCustomClueData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal);

        var payload = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string?>>>(json, JsonSerializerOptions)
            ?? [];

        return payload.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(entry.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public static string? SerializeCustomClueData(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> data)
    {
        if (data.Count == 0) return null;

        var payload = data.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);

        return JsonSerializer.Serialize(payload, JsonSerializerOptions);
    }

    public static RankedCountryRecord ApplyCustomClueData(
        RankedCountryRecord country,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> customClueData)
    {
        if (customClueData.Count == 0) return country;

        var values = new Dictionary<string, string?>(country.Values, StringComparer.Ordinal);
        foreach (var (clueId, rows) in customClueData)
        {
            if (rows.TryGetValue(country.CountryId, out var value))
            {
                values[clueId] = value;
            }
        }

        return new RankedCountryRecord(country.CountryId, country.Name, country.Lat, country.Lon, values);
    }
}
