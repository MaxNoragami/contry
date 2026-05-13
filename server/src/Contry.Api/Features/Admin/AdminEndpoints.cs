using System.Text.Json;
using Contry.Api.Common.EndpointFilters;
using Contry.Application.Ranked;
using Contry.Domain.Ranked;
using FluentValidation;

namespace Contry.Api.Features.Admin;

public static class AdminEndpoints
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        group.MapPut("/ranked-challenges/today/target", SetTodayRankedTargetAsync)
            .WithValidation<SetTodayRankedTargetRequest>()
            .RequireXsrf()
            .WithName("SetTodayRankedTarget")
            .WithSummary("Reset today's ranked country.")
            .WithDescription("Sets today's ranked target country and clears today's ranked sessions so users can replay the new challenge.")
            .Produces<SetTodayRankedTargetResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/leaderboards/ranked", ResetRankedLeaderboardAsync)
            .RequireXsrf()
            .WithName("ResetRankedLeaderboard")
            .WithSummary("Reset the ranked leaderboard.")
            .WithDescription("Deletes ranked user stats, clue usage, discovery data, sessions, and guesses while preserving ranked challenge history.")
            .Produces<ResetRankedLeaderboardResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> SetTodayRankedTargetAsync(
        SetTodayRankedTargetRequest request,
        IRankedStore rankedStore,
        IRankedDatasetProvider rankedDatasetProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var normalizedCountryId = request.CountryId.Trim().ToUpperInvariant();

        var targetCountry = await rankedDatasetProvider.FindCountryAsync(normalizedCountryId, cancellationToken);
        if (targetCountry is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["countryId"] = ["The specified country does not exist in the ranked dataset."]
            });
        }

        var challenge = await rankedStore.FindChallengeByDateAsync(today, cancellationToken);
        if (challenge is null)
        {
            var challengeDefinition = await rankedDatasetProvider.GetChallengeDefinitionAsync(today, cancellationToken);
            challenge = new RankedChallenge
            {
                Id = Guid.NewGuid(),
                ChallengeDateUtc = today,
                TargetCountryId = normalizedCountryId,
                ClueSetJson = JsonSerializer.Serialize(challengeDefinition.Clues, JsonSerializerOptions),
                CreatedAtUtc = timeProvider.GetUtcNow()
            };

            await rankedStore.AddChallengeAsync(challenge, cancellationToken);
        }
        else
        {
            challenge.TargetCountryId = normalizedCountryId;
            await rankedStore.UpdateChallengeAsync(challenge, cancellationToken);
        }

        await rankedStore.DeleteSessionsByDateAsync(today, cancellationToken);
        return TypedResults.Ok(new SetTodayRankedTargetResponse(
            today,
            normalizedCountryId,
            targetCountry.Name,
            true));
    }

    private static async Task<IResult> ResetRankedLeaderboardAsync(
        IRankedStore rankedStore,
        CancellationToken cancellationToken)
    {
        await rankedStore.ClearAllRankedDataAsync(cancellationToken);
        return TypedResults.Ok(new ResetRankedLeaderboardResponse(true, true));
    }
}

public sealed record SetTodayRankedTargetRequest(string CountryId);

public sealed record SetTodayRankedTargetResponse(
    DateOnly ChallengeDateUtc,
    string TargetCountryId,
    string TargetCountryName,
    bool SessionsReset);

public sealed record ResetRankedLeaderboardResponse(
    bool LeaderboardReset,
    bool PreservedChallengeHistory);

public sealed class SetTodayRankedTargetRequestValidator : AbstractValidator<SetTodayRankedTargetRequest>
{
    public SetTodayRankedTargetRequestValidator()
    {
        RuleFor(request => request.CountryId)
            .NotEmpty()
            .MaximumLength(16);
    }
}
