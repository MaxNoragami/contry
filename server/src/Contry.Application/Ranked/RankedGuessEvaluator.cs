using System.Globalization;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Ranked;

public sealed class RankedGuessEvaluator
{
    public IReadOnlyList<RankedClueResult> Evaluate(
        RankedCountryRecord guess,
        RankedCountryRecord target,
        IReadOnlyList<RankedClueDefinition> clues,
        DateOnly challengeDateUtc)
    {
        var results = new List<RankedClueResult>(clues.Count);

        foreach (var clue in clues)
        {
            results.Add(EvaluateClue(clue, guess, target, challengeDateUtc));
        }

        return results;
    }

    private static RankedClueResult EvaluateClue(
        RankedClueDefinition clue,
        RankedCountryRecord guess,
        RankedCountryRecord target,
        DateOnly challengeDateUtc)
    {
        if (clue.Type == RankedClueType.Numeric)
        {
            return EvaluateNumeric(clue, ResolveValue(clue, guess, challengeDateUtc), ResolveValue(clue, target, challengeDateUtc));
        }

        return clue.Id switch
        {
            "hemisphere" => EvaluateHemisphere(clue, guess.Lat, target.Lat),
            "coordinates" => EvaluateCoordinates(clue, guess.Lat, guess.Lon, target.Lat, target.Lon),
            _ when clue.Comparator == "exact" => EvaluateCategorical(clue, ResolveValue(clue, guess, challengeDateUtc), ResolveValue(clue, target, challengeDateUtc)),
            _ => EvaluateNumeric(clue, ResolveValue(clue, guess, challengeDateUtc), ResolveValue(clue, target, challengeDateUtc))
        };
    }

    private static string? ResolveValue(RankedClueDefinition clue, RankedCountryRecord country, DateOnly challengeDateUtc)
    {
        if (clue.Group == "temperature_avg_c")
        {
            var monthKey = $"temperature_avg_c_m{challengeDateUtc.Month:00}";
            return country.Values.TryGetValue(monthKey, out var monthValue) ? monthValue : null;
        }

        return country.Values.TryGetValue(clue.Id, out var value) ? value : null;
    }

    private static RankedClueResult EvaluateCategorical(RankedClueDefinition clue, string? guessValue, string? targetValue)
    {
        var guessMissing = string.IsNullOrWhiteSpace(guessValue);
        var targetMissing = string.IsNullOrWhiteSpace(targetValue);

        if (guessMissing)
        {
            return new RankedClueResult(
                clue.Id,
                clue.Label,
                "NO DATA",
                targetMissing ? RankedChipTone.Green : RankedChipTone.Red,
                RankedClueKind.Text,
                null);
        }

        var isMatch = !targetMissing && string.Equals(guessValue!.Trim(), targetValue!.Trim(), StringComparison.OrdinalIgnoreCase);
        return new RankedClueResult(
            clue.Id,
            clue.Label,
            guessValue!,
            isMatch ? RankedChipTone.Green : RankedChipTone.Red,
            RankedClueKind.Text,
            null);
    }

    private static RankedClueResult EvaluateNumeric(RankedClueDefinition clue, string? guessValue, string? targetValue)
    {
        var guessMissing = string.IsNullOrWhiteSpace(guessValue);
        var targetMissing = string.IsNullOrWhiteSpace(targetValue);

        if (guessMissing && targetMissing)
        {
            return new RankedClueResult(clue.Id, clue.Label, "NO DATA", RankedChipTone.Green, RankedClueKind.Text, null);
        }

        if (guessMissing)
        {
            return new RankedClueResult(
                clue.Id,
                clue.Label,
                "NO DATA",
                RankedChipTone.Red,
                RankedClueKind.Text,
                targetMissing ? null : RankedGuessTrend.Higher);
        }

        if (targetMissing)
        {
            var parsedGuessValue = ParseDouble(guessValue!);
            return new RankedClueResult(
                clue.Id,
                clue.Label,
                parsedGuessValue is null ? guessValue! : FormatNumeric(parsedGuessValue.Value, clue.UnitSymbol),
                RankedChipTone.Red,
                RankedClueKind.Numeric,
                RankedGuessTrend.Lower);
        }

        var guessNumber = ParseDouble(guessValue!);
        var targetNumber = ParseDouble(targetValue!);

        if (guessNumber is null || targetNumber is null)
        {
            return new RankedClueResult(clue.Id, clue.Label, guessValue!, RankedChipTone.Red, RankedClueKind.Numeric, null);
        }

        var exact = guessNumber.Value == targetNumber.Value;
        var errorPercentage = Math.Abs(guessNumber.Value - targetNumber.Value) / Math.Max(Math.Abs(targetNumber.Value), 1e-9d) * 100d;
        var tone = errorPercentage <= 10d
            ? RankedChipTone.Green
            : errorPercentage <= 35d
                ? RankedChipTone.Yellow
                : RankedChipTone.Red;

        return new RankedClueResult(
            clue.Id,
            clue.Label,
            FormatNumeric(guessNumber.Value, clue.UnitSymbol),
            tone,
            RankedClueKind.Numeric,
            exact ? null : guessNumber.Value > targetNumber.Value ? RankedGuessTrend.Lower : RankedGuessTrend.Higher);
    }

    private static RankedClueResult EvaluateHemisphere(RankedClueDefinition clue, double guessLat, double targetLat)
    {
        var guessHemisphere = guessLat >= 0 ? "NORTHERN" : "SOUTHERN";
        var targetHemisphere = targetLat >= 0 ? "NORTHERN" : "SOUTHERN";

        return new RankedClueResult(
            clue.Id,
            clue.Label,
            guessHemisphere,
            guessHemisphere == targetHemisphere ? RankedChipTone.Green : RankedChipTone.Red,
            RankedClueKind.Text,
            null);
    }

    private static RankedClueResult EvaluateCoordinates(RankedClueDefinition clue, double guessLat, double guessLon, double targetLat, double targetLon)
    {
        if (guessLat == targetLat && guessLon == targetLon)
        {
            return new RankedClueResult(clue.Id, clue.Label, "\u2713", RankedChipTone.Green, RankedClueKind.Direction, null);
        }

        var latitudeDelta = targetLat - guessLat;
        var longitudeDelta = NormalizeLongitudeDelta(targetLon - guessLon);

        return new RankedClueResult(
            clue.Id,
            clue.Label,
            DeltasToDirection(latitudeDelta, longitudeDelta),
            RankedChipTone.Blue,
            RankedClueKind.Direction,
            null);
    }

    private static double NormalizeLongitudeDelta(double delta)
        => ((delta + 540d) % 360d) - 180d;

    private static string DeltasToDirection(double latDelta, double lonDelta)
    {
        var latMagnitude = Math.Abs(latDelta);
        var lonMagnitude = Math.Abs(lonDelta);

        if (latMagnitude == 0d)
        {
            return lonDelta > 0d ? "E" : "W";
        }

        if (lonMagnitude == 0d)
        {
            return latDelta > 0d ? "N" : "S";
        }

        var minorAxisRatio = Math.Min(latMagnitude, lonMagnitude) / Math.Max(latMagnitude, lonMagnitude);
        if (minorAxisRatio < 0.3d)
        {
            return latMagnitude > lonMagnitude
                ? latDelta > 0d ? "N" : "S"
                : lonDelta > 0d ? "E" : "W";
        }

        return $"{(latDelta > 0d ? "N" : "S")}{(lonDelta > 0d ? "E" : "W")}";
    }

    private static double? ParseDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string FormatNumeric(double value, string? unitSymbol)
    {
        if (unitSymbol == "degC")
        {
            return $"{value.ToString("0.0", CultureInfo.InvariantCulture)} °C";
        }

        var numberString = value.ToString(CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(unitSymbol)
            ? numberString
            : $"{numberString} {unitSymbol}";
    }
}
