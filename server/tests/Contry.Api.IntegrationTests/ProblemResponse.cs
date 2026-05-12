using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contry.Api.IntegrationTests;

public sealed class ProblemResponse
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? TraceId { get; init; }

    public Dictionary<string, string[]>? Errors { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}
