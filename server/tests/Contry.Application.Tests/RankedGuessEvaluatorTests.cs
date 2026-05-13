using Contry.Application.Ranked;
using Contry.Application.Ranked.Models;

namespace Contry.Application.Tests;

public sealed class RankedGuessEvaluatorTests
{
    private readonly RankedGuessEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_NumericClue_UsesSameBandsAndTrendRules()
    {
        var clue = new RankedClueDefinition("population", "Population", "", "users", RankedClueType.Numeric, "higher_lower", null, null);
        var guess = new RankedCountryRecord("MD", "Moldova", 47d, 28d, new Dictionary<string, string?> { ["population"] = "200" });
        var target = new RankedCountryRecord("RO", "Romania", 45d, 25d, new Dictionary<string, string?> { ["population"] = "100" });

        var result = _evaluator.Evaluate(guess, target, [clue], new DateOnly(2026, 5, 13)).Single();

        Assert.Equal(RankedChipTone.Red, result.Tone);
        Assert.Equal(RankedGuessTrend.Lower, result.Trend);
        Assert.Equal("200", result.Value);
    }

    [Fact]
    public void Evaluate_NumericClue_DoesNotDowngradeToExactWhenComparatorIsWrong()
    {
        var clue = new RankedClueDefinition("population", "Population", "", "users", RankedClueType.Numeric, "exact", null, null);
        var guess = new RankedCountryRecord("MD", "Moldova", 47d, 28d, new Dictionary<string, string?> { ["population"] = "200" });
        var target = new RankedCountryRecord("RO", "Romania", 45d, 25d, new Dictionary<string, string?> { ["population"] = "100" });

        var result = _evaluator.Evaluate(guess, target, [clue], new DateOnly(2026, 5, 13)).Single();

        Assert.Equal(RankedClueKind.Numeric, result.Kind);
        Assert.Equal(RankedGuessTrend.Lower, result.Trend);
        Assert.Equal(RankedChipTone.Red, result.Tone);
    }

    [Fact]
    public void Evaluate_CategoricalMissingData_MatchesClientSemantics()
    {
        var clue = new RankedClueDefinition("continent", "Continent", "", "compass", RankedClueType.Categorical, "exact", null, null);
        var guess = new RankedCountryRecord("MD", "Moldova", 47d, 28d, new Dictionary<string, string?>());
        var target = new RankedCountryRecord("RO", "Romania", 45d, 25d, new Dictionary<string, string?> { ["continent"] = "Europe" });

        var result = _evaluator.Evaluate(guess, target, [clue], new DateOnly(2026, 5, 13)).Single();

        Assert.Equal("NO DATA", result.Value);
        Assert.Equal(RankedChipTone.Red, result.Tone);
        Assert.Equal(RankedClueKind.Text, result.Kind);
    }

    [Fact]
    public void Evaluate_TemperatureGroup_ResolvesCurrentMonthDataset()
    {
        var clue = new RankedClueDefinition("temperature_avg_c", "Average Temperature", "", "thermometer", RankedClueType.Numeric, "higher_lower", "temperature_avg_c", "degC");
        var guess = new RankedCountryRecord("MD", "Moldova", 47d, 28d, new Dictionary<string, string?> { ["temperature_avg_c_m05"] = "12.5" });
        var target = new RankedCountryRecord("RO", "Romania", 45d, 25d, new Dictionary<string, string?> { ["temperature_avg_c_m05"] = "12.5" });

        var result = _evaluator.Evaluate(guess, target, [clue], new DateOnly(2026, 5, 13)).Single();

        Assert.Equal(RankedChipTone.Green, result.Tone);
        Assert.Equal("12.5 °C", result.Value);
        Assert.Null(result.Trend);
    }

    [Fact]
    public void Evaluate_Coordinates_UsesMapRelativeDirection()
    {
        var clue = new RankedClueDefinition("coordinates", "Coordinates", "", "navigation", RankedClueType.Computed, "coordinates", null, null);
        var guess = new RankedCountryRecord("FJ", "Fiji", -17.7d, 178.1d, new Dictionary<string, string?>());
        var target = new RankedCountryRecord("WS", "Samoa", -13.7d, -172.1d, new Dictionary<string, string?>());

        var result = _evaluator.Evaluate(guess, target, [clue], new DateOnly(2026, 5, 13)).Single();

        Assert.Equal(RankedChipTone.Blue, result.Tone);
        Assert.Equal("NE", result.Value);
        Assert.Equal(RankedClueKind.Direction, result.Kind);
    }
}
