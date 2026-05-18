using System.Text.Json;
using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Ranked.Challenges.Handlers;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Challenges.Queries;
using Contry.Application.Ranked.Models;
using Contry.Domain.Clues;
using Contry.Domain.Ranked;
using Contry.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.Features.Ranked.Challenges;

public static class RankedChallengeEndpoints
{
    public static IEndpointRouteBuilder MapRankedChallengeEndpoints(this IEndpointRouteBuilder app)
    {
        var challenges = app.MapGroup("/ranked/challenges")
            .WithTags("Ranked");

        challenges.MapGet("/current", GetCurrentRankedChallengeHandler.HandleAsync)
            .RequireAuthorization()
            .WithName("GetCurrentRankedChallenge")
            .WithSummary("Get the current ranked challenge metadata.")
            .WithDescription("Returns the current UTC daily ranked clue set without exposing the hidden target country.")
            .Produces<RankedChallengeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        challenges.MapGet("/{date}", GetAdminChallengeEditorAsync)
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"))
            .WithName("GetRankedChallengeByDate")
            .WithSummary("Admin only: get ranked challenge editor data for a UTC date.")
            .Produces<AdminRankedChallengeEditorResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        challenges.MapPut("/{date}", SaveAdminChallengeAsync)
            .WithValidation<SaveAdminRankedChallengeRequest>()
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"))
            .RequireXsrf()
            .WithName("SaveRankedChallengeByDate")
            .WithSummary("Admin only: save or update a ranked challenge for a UTC date.")
            .Produces<AdminRankedChallengeEditorResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        challenges.MapDelete("/{date}", DeleteAdminChallengeAsync)
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"))
            .RequireXsrf()
            .WithName("DeleteRankedChallengeByDate")
            .WithSummary("Admin only: delete/reset a ranked challenge for a UTC date.")
            .Produces<DeleteAdminRankedChallengeResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetAdminChallengeEditorAsync(
        string date,
        ContryDbContext dbContext,
        IRankedStore rankedStore,
        IRankedDatasetProvider rankedDatasetProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var challengeDateUtc, out var error))
        {
            return TypedResults.ValidationProblem(error!);
        }

        var response = await BuildEditorResponseAsync(dbContext, rankedStore, rankedDatasetProvider, timeProvider, challengeDateUtc, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> SaveAdminChallengeAsync(
        string date,
        SaveAdminRankedChallengeRequest request,
        ContryDbContext dbContext,
        IRankedStore rankedStore,
        IRankedDatasetProvider rankedDatasetProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var challengeDateUtc, out var error))
        {
            return TypedResults.ValidationProblem(error!);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var countries = await rankedDatasetProvider.GetCountriesAsync(cancellationToken);
        var targetCountry = countries.FirstOrDefault(country => string.Equals(country.CountryId, request.TargetCountryId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (targetCountry is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetCountryId"] = ["The specified country does not exist in the ranked dataset."]
            });
        }

        var builtinClues = await rankedDatasetProvider.GetBuiltinClueCatalogAsync(cancellationToken);
        var publishedCluePacks = await dbContext.CluePacks
            .AsNoTracking()
            .Join(dbContext.Users.AsNoTracking(), pack => pack.OwnerId, user => user.Id, (pack, user) => new PublishedCluePackEntry(pack, user.Username))
            .ToListAsync(cancellationToken);
        publishedCluePacks = publishedCluePacks.OrderBy(entry => entry.Pack.Label, StringComparer.Ordinal).ToList();

        var availableClues = BuildAvailableClueCatalog(builtinClues, publishedCluePacks);
        var requestedClues = ResolveRequestedClues(request.ClueIds, availableClues);
        if (requestedClues.Error is not null)
        {
            return TypedResults.ValidationProblem(requestedClues.Error);
        }

        var customClueData = BuildCustomClueData(requestedClues.Clues, publishedCluePacks);
        var now = timeProvider.GetUtcNow();
        var challenge = await rankedStore.FindChallengeByDateAsync(challengeDateUtc, cancellationToken);

        if (challenge is null)
        {
            challenge = new RankedChallenge
            {
                Id = Guid.NewGuid(),
                ChallengeDateUtc = challengeDateUtc,
                TargetCountryId = targetCountry.CountryId,
                ClueSetJson = RankedChallengeSerialization.SerializeClues(requestedClues.Clues),
                CustomClueDataJson = RankedChallengeSerialization.SerializeCustomClueData(customClueData),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            await rankedStore.AddChallengeAsync(challenge, cancellationToken);
        }
        else
        {
            challenge.TargetCountryId = targetCountry.CountryId;
            challenge.ClueSetJson = RankedChallengeSerialization.SerializeClues(requestedClues.Clues);
            challenge.CustomClueDataJson = RankedChallengeSerialization.SerializeCustomClueData(customClueData);
            challenge.UpdatedAtUtc = now;
            await rankedStore.UpdateChallengeAsync(challenge, cancellationToken);
        }

        var sessionsReset = false;
        if (request.ResetSessions || challengeDateUtc <= today)
        {
            await rankedStore.DeleteSessionsByDateAndRebuildStatsAsync(challengeDateUtc, cancellationToken);
            sessionsReset = true;
        }

        var response = await BuildEditorResponseAsync(dbContext, rankedStore, rankedDatasetProvider, timeProvider, challengeDateUtc, cancellationToken, sessionsReset);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> DeleteAdminChallengeAsync(
        string date,
        IRankedStore rankedStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var challengeDateUtc, out var error))
        {
            return TypedResults.ValidationProblem(error!);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var resetSessions = challengeDateUtc <= today;

        if (resetSessions)
        {
            await rankedStore.DeleteSessionsByDateAndRebuildStatsAsync(challengeDateUtc, cancellationToken);
        }

        await rankedStore.DeleteChallengeByDateAsync(challengeDateUtc, cancellationToken);
        return TypedResults.Ok(new DeleteAdminRankedChallengeResponse(challengeDateUtc, true, resetSessions));
    }

    private static async Task<AdminRankedChallengeEditorResponse> BuildEditorResponseAsync(
        ContryDbContext dbContext,
        IRankedStore rankedStore,
        IRankedDatasetProvider rankedDatasetProvider,
        TimeProvider timeProvider,
        DateOnly challengeDateUtc,
        CancellationToken cancellationToken,
        bool sessionsReset = false)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var persisted = await rankedStore.FindChallengeByDateAsync(challengeDateUtc, cancellationToken);
        RankedCountryRecord targetCountry;
        IReadOnlyList<RankedClueDefinition> selectedClues;

        if (persisted is null)
        {
            var generated = await rankedDatasetProvider.GetChallengeDefinitionAsync(challengeDateUtc, cancellationToken);
            targetCountry = (await rankedDatasetProvider.FindCountryAsync(generated.TargetCountryId, cancellationToken))!;
            selectedClues = generated.Clues;
        }
        else
        {
            targetCountry = (await rankedDatasetProvider.FindCountryAsync(persisted.TargetCountryId, cancellationToken))!;
            selectedClues = RankedChallengeSerialization.DeserializeClues(persisted.ClueSetJson);
        }

        var countries = await rankedDatasetProvider.GetCountriesAsync(cancellationToken);
        var builtinClues = await rankedDatasetProvider.GetBuiltinClueCatalogAsync(cancellationToken);
        var publishedCluePacks = await dbContext.CluePacks
            .AsNoTracking()
            .Join(dbContext.Users.AsNoTracking(), pack => pack.OwnerId, user => user.Id, (pack, user) => new PublishedCluePackEntry(pack, user.Username))
            .ToListAsync(cancellationToken);
        publishedCluePacks = publishedCluePacks.OrderBy(entry => entry.Pack.Label, StringComparer.Ordinal).ToList();

        var availableClues = BuildAvailableClueCatalog(builtinClues, publishedCluePacks);

        return new AdminRankedChallengeEditorResponse(
            challengeDateUtc,
            challengeDateUtc == today ? "current" : challengeDateUtc == today.AddDays(1) ? "tomorrow" : "scheduled",
            persisted is not null,
            targetCountry.CountryId,
            targetCountry.Name,
            selectedClues.Select(AdminRankedClueResponse.FromModel).ToList(),
            countries.Select(country => new RankedCountryOptionResponse(country.CountryId, country.Name)).ToList(),
            availableClues.Select(clue => new AdminRankedClueOptionResponse(
                clue.Id,
                clue.Label,
                clue.Description,
                clue.Icon,
                clue.Type switch
                {
                    RankedClueType.Numeric => "numeric",
                    RankedClueType.Categorical => "categorical",
                    _ => "computed"
                },
                clue.Comparator,
                clue.Group,
                clue.UnitSymbol,
                clue.Source == RankedClueSource.Published ? "published" : "builtin",
                clue.RemoteId,
                clue.OwnerUsername,
                clue.Categories)).ToList(),
            challengeDateUtc <= today,
            challengeDateUtc > today,
            sessionsReset);
    }

    private static IReadOnlyList<AdminAvailableClue> BuildAvailableClueCatalog(
        IReadOnlyList<RankedClueDefinition> builtinClues,
        IReadOnlyList<PublishedCluePackEntry> publishedCluePacks)
    {
        var clues = new List<AdminAvailableClue>();

        clues.AddRange(builtinClues.Select(clue => new AdminAvailableClue(
            clue.Id,
            clue.Label,
            clue.Description,
            clue.Icon,
            clue.Type,
            clue.Comparator,
            clue.Group,
            clue.UnitSymbol,
            RankedClueSource.Builtin,
            null,
            null)));

        foreach (var entry in publishedCluePacks)
        {
            var categories = string.IsNullOrWhiteSpace(entry.Pack.CategoriesJson)
                ? null
                : JsonSerializer.Deserialize<List<string>>(entry.Pack.CategoriesJson) ?? [];

            clues.Add(new AdminAvailableClue(
                entry.Pack.DatasetId,
                entry.Pack.Label,
                entry.Pack.Description,
                entry.Pack.Icon,
                ParseRankedClueType(entry.Pack.Type),
                entry.Pack.Comparator,
                null,
                entry.Pack.UnitSymbol,
                RankedClueSource.Published,
                (Guid)entry.Pack.Id,
                (string)entry.OwnerUsername,
                categories));
        }

        return clues;
    }

    private static (IReadOnlyList<RankedClueDefinition> Clues, Dictionary<string, string[]>? Error) ResolveRequestedClues(
        IReadOnlyList<string> clueIds,
        IReadOnlyList<AdminAvailableClue> availableClues)
    {
        if (clueIds.Count != 5)
        {
            return ([], new Dictionary<string, string[]> { ["clueIds"] = ["Exactly 5 clues are required."] });
        }

        if (clueIds.Distinct(StringComparer.Ordinal).Count() != clueIds.Count)
        {
            return ([], new Dictionary<string, string[]> { ["clueIds"] = ["Duplicate clues are not allowed."] });
        }

        var byId = availableClues.ToDictionary(clue => clue.Id, clue => clue, StringComparer.Ordinal);
        var clues = new List<RankedClueDefinition>(5);
        foreach (var clueId in clueIds)
        {
            if (!byId.TryGetValue(clueId, out var clue))
            {
                return ([], new Dictionary<string, string[]> { ["clueIds"] = [$"Unknown clue '{clueId}'."] });
            }

            clues.Add(new RankedClueDefinition(
                clue.Id,
                clue.Label,
                clue.Description,
                clue.Icon,
                clue.Type,
                clue.Comparator,
                clue.Group,
                clue.UnitSymbol,
                clue.Source,
                clue.RemoteId));
        }

        return (clues, null);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> BuildCustomClueData(
        IReadOnlyList<RankedClueDefinition> selectedClues,
        IReadOnlyList<PublishedCluePackEntry> publishedCluePacks)
    {
        var selectedRemoteIds = selectedClues
            .Where(clue => clue.Source == RankedClueSource.Published && clue.RemoteId is not null)
            .Select(clue => clue.RemoteId!.Value)
            .ToHashSet();

        var result = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal);
        foreach (var entry in publishedCluePacks.Where(item => selectedRemoteIds.Contains(item.Pack.Id)))
        {
            var rows = JsonSerializer.Deserialize<List<CluePackRowPayload>>(entry.Pack.RowsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? [];
            result[entry.Pack.DatasetId] = rows.ToDictionary(
                row => row.CountryId,
                row => row.Value?.ToString(),
                StringComparer.Ordinal);
        }

        return result;
    }

    private static RankedClueType ParseRankedClueType(string type)
        => type switch
        {
            "numeric" => RankedClueType.Numeric,
            "categorical" => RankedClueType.Categorical,
            _ => RankedClueType.Computed
        };

    private static bool TryParseDate(string rawDate, out DateOnly challengeDateUtc, out Dictionary<string, string[]>? error)
    {
        if (DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", out challengeDateUtc))
        {
            error = null;
            return true;
        }

        error = new Dictionary<string, string[]>
        {
            ["date"] = ["Date must use yyyy-MM-dd."]
        };
        return false;
    }
}

public sealed record SaveAdminRankedChallengeRequest(
    string TargetCountryId,
    IReadOnlyList<string> ClueIds,
    bool ResetSessions = false);

public sealed record DeleteAdminRankedChallengeResponse(
    DateOnly ChallengeDateUtc,
    bool Deleted,
    bool SessionsReset);

public sealed record AdminRankedChallengeEditorResponse(
    DateOnly ChallengeDateUtc,
    string Scope,
    bool IsPersisted,
    string TargetCountryId,
    string TargetCountryName,
    IReadOnlyList<AdminRankedClueResponse> SelectedClues,
    IReadOnlyList<RankedCountryOptionResponse> Countries,
    IReadOnlyList<AdminRankedClueOptionResponse> AvailableClues,
    bool CanResetSessions,
    bool CanDeleteSchedule,
    bool SessionsReset);

public sealed record RankedCountryOptionResponse(string CountryId, string Name);

public sealed record AdminRankedClueResponse(
    string Id,
    string Label,
    string Description,
    string Icon,
    string Type,
    string Comparator,
    string? Group,
    string? UnitSymbol,
    string Source)
{
    public static AdminRankedClueResponse FromModel(RankedClueDefinition clue)
        => new(clue.Id, clue.Label, clue.Description, clue.Icon, ToType(clue.Type), clue.Comparator, clue.Group, clue.UnitSymbol, clue.Source == RankedClueSource.Published ? "published" : "builtin");

    private static string ToType(RankedClueType type)
        => type switch
        {
            RankedClueType.Numeric => "numeric",
            RankedClueType.Categorical => "categorical",
            _ => "computed"
        };
}

public sealed record AdminRankedClueOptionResponse(
    string Id,
    string Label,
    string Description,
    string Icon,
    string Type,
    string Comparator,
    string? Group,
    string? UnitSymbol,
    string Source,
    Guid? RemoteId,
    string? OwnerUsername,
    IReadOnlyList<string>? Categories);

internal sealed record AdminAvailableClue(
    string Id,
    string Label,
    string Description,
    string Icon,
    RankedClueType Type,
    string Comparator,
    string? Group,
    string? UnitSymbol,
    RankedClueSource Source,
    Guid? RemoteId,
    string? OwnerUsername,
    IReadOnlyList<string>? Categories = null);

internal sealed record PublishedCluePackEntry(CluePack Pack, string OwnerUsername);

internal sealed record CluePackRowPayload(string CountryId, object? Value);

public sealed class SaveAdminRankedChallengeRequestValidator : AbstractValidator<SaveAdminRankedChallengeRequest>
{
    public SaveAdminRankedChallengeRequestValidator()
    {
        RuleFor(request => request.TargetCountryId).NotEmpty().MaximumLength(16);
        RuleFor(request => request.ClueIds).NotEmpty().Must(clueIds => clueIds.Count == 5);
        RuleForEach(request => request.ClueIds).NotEmpty().MaximumLength(96);
    }
}
