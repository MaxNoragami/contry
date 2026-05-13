using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;
using Contry.Infrastructure.Datasets;

namespace Contry.Infrastructure.Ranked;

public sealed class FileRankedDatasetProvider(BuiltInDatasetCatalog builtInDatasetCatalog) : IRankedDatasetProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DefaultClueIds = ["hemisphere", "continent", "temperature_avg_c", "population", "coordinates"];

    private readonly BuiltInDatasetCatalog _builtInDatasetCatalog = builtInDatasetCatalog;
    private readonly Lock _loadLock = new();
    private DatasetCache? _cache;

    public Task<RankedChallengeDefinition> GetChallengeDefinitionAsync(DateOnly challengeDateUtc, CancellationToken cancellationToken)
    {
        var cache = GetOrLoadCache();
        var target = SelectTargetCountry(cache.Countries, challengeDateUtc);
        return Task.FromResult(new RankedChallengeDefinition(challengeDateUtc, target.CountryId, cache.Clues));
    }

    public Task<RankedCountryRecord?> FindCountryAsync(string countryId, CancellationToken cancellationToken)
    {
        var cache = GetOrLoadCache();
        cache.CountriesById.TryGetValue(countryId.Trim().ToUpperInvariant(), out var country);
        return Task.FromResult(country);
    }

    private DatasetCache GetOrLoadCache()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        lock (_loadLock)
        {
            _cache ??= LoadCacheAsync().GetAwaiter().GetResult();
            return _cache;
        }
    }

    private async Task<DatasetCache> LoadCacheAsync()
    {
        var manifestDocument = await _builtInDatasetCatalog.FindByPathAsync("/datasets/manifest.json", CancellationToken.None)
            ?? throw new InvalidOperationException("Failed to load canonical dataset manifest.");
        var manifest = JsonSerializer.Deserialize<ManifestDocument>(manifestDocument.Content, JsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to load ranked dataset manifest.");

        var countries = await LoadCountriesAsync();
        var countriesById = countries.ToDictionary(country => country.CountryId, country => country.ToImmutable(), StringComparer.Ordinal);

        foreach (var clue in manifest.Clues.Where(clue => clue.Source == "builtin" && !clue.Computed && clue.DataPath is not null))
        {
            var rows = await LoadDatasetRowsAsync(clue.DataPath!);

            foreach (var row in rows)
            {
                var mutableCountry = countries.FirstOrDefault(country => country.CountryId == row.CountryId);
                if (mutableCountry is not null)
                {
                    mutableCountry.MutableValues[clue.DatasetId] = row.Value;
                }
            }
        }

        var publicClues = BuildPublicClues(manifest);

        countriesById = countries.ToDictionary(country => country.CountryId, country => country.ToImmutable(), StringComparer.Ordinal);

        return new DatasetCache(countries, countriesById, publicClues);
    }

    private async Task<List<MutableCountryRecord>> LoadCountriesAsync()
    {
        var countriesDocument = await _builtInDatasetCatalog.FindByPathAsync("/datasets/base/countries.csv", CancellationToken.None)
            ?? throw new InvalidOperationException("Failed to load canonical countries dataset.");
        var lines = countriesDocument.Content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var countries = new List<MutableCountryRecord>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            countries.Add(new MutableCountryRecord(
                columns[0].Trim().ToUpperInvariant(),
                columns[1].Trim(),
                double.Parse(columns[2], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(columns[3], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return countries.OrderBy(country => country.CountryId, StringComparer.Ordinal).ToList();
    }

    private async Task<List<DatasetValueRow>> LoadDatasetRowsAsync(string relativePath)
    {
        var normalizedPath = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
        var document = await _builtInDatasetCatalog.FindByPathAsync(normalizedPath, CancellationToken.None)
            ?? throw new InvalidOperationException($"Failed to load canonical dataset asset '{normalizedPath}'.");
        var lines = document.Content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var rows = new List<DatasetValueRow>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            rows.Add(new DatasetValueRow(columns[0].Trim().ToUpperInvariant(), columns[1].Trim()));
        }

        return rows;
    }

    private static IReadOnlyList<RankedClueDefinition> BuildPublicClues(ManifestDocument manifest)
    {
        var clues = new List<RankedClueDefinition>(DefaultClueIds.Length);

        foreach (var clueId in DefaultClueIds)
        {
            if (clueId == "temperature_avg_c")
            {
                var monthlyClue = manifest.Clues.First(clue => clue.Group == "temperature_avg_c" && clue.Month == 1);
                clues.Add(new RankedClueDefinition(
                    "temperature_avg_c",
                    "Average Temperature",
                    monthlyClue.Description ?? string.Empty,
                    monthlyClue.Icon ?? string.Empty,
                    RankedClueType.Numeric,
                    monthlyClue.Comparator ?? "higher_lower",
                    "temperature_avg_c",
                    monthlyClue.UnitSymbol));
                continue;
            }

            var clue = manifest.Clues.First(entry => entry.Id == clueId);
            clues.Add(new RankedClueDefinition(
                clue.Id,
                clue.Label ?? clue.Id,
                clue.Description ?? string.Empty,
                clue.Icon ?? string.Empty,
                ParseClueType(clue.Type),
                clue.Comparator ?? "exact",
                clue.Group,
                clue.UnitSymbol));
        }

        return clues;
    }

    private static RankedClueType ParseClueType(string type)
        => type switch
        {
            "numeric" => RankedClueType.Numeric,
            "categorical" => RankedClueType.Categorical,
            _ => RankedClueType.Computed
        };

    private static RankedCountryRecord SelectTargetCountry(IReadOnlyList<MutableCountryRecord> countries, DateOnly challengeDateUtc)
    {
        var input = Encoding.UTF8.GetBytes(challengeDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(input);
        var index = BitConverter.ToUInt32(hash, 0) % countries.Count;
        return countries[(int)index].ToImmutable();
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private sealed record DatasetCache(
        IReadOnlyList<MutableCountryRecord> Countries,
        IReadOnlyDictionary<string, RankedCountryRecord> CountriesById,
        IReadOnlyList<RankedClueDefinition> Clues);

    private sealed class MutableCountryRecord(string countryId, string name, double lat, double lon)
    {
        public string CountryId { get; } = countryId;

        public string Name { get; } = name;

        public double Lat { get; } = lat;

        public double Lon { get; } = lon;

        public Dictionary<string, string?> MutableValues { get; } = new(StringComparer.Ordinal);

        public RankedCountryRecord ToImmutable()
            => new(CountryId, Name, Lat, Lon, new Dictionary<string, string?>(MutableValues, StringComparer.Ordinal));
    }

    private sealed record DatasetValueRow(string CountryId, string? Value);

    private sealed record ManifestDocument(IReadOnlyList<ManifestClueDocument> Clues);

    private sealed record ManifestClueDocument(
        string Id,
        [property: JsonPropertyName("dataset_id")]
        string DatasetId,
        string Source,
        string Type,
        bool Computed,
        [property: JsonPropertyName("data_path")]
        string? DataPath,
        string? Group,
        int? Month,
        string? Label,
        string? Description,
        string? Icon,
        [property: JsonPropertyName("unit_symbol")]
        string? UnitSymbol,
        string? Comparator);
}
