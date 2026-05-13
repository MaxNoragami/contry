using System.Text.Json;
using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Ranked.Challenges.Handlers;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Challenges.Queries;
using Contry.Domain.Ranked;
using FluentValidation;

namespace Contry.Api.Features.Ranked.Challenges;

public static class RankedChallengeEndpoints
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

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

        challenges.MapPut("/today/target", SetTodayRankedTargetAsync)
            .WithValidation<SetTodayRankedTargetRequest>()
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"))
            .RequireXsrf()
            .WithName("SetTodayRankedTarget")
            .WithSummary("Admin only: set today's ranked target country.")
            .WithDescription("Available only to admins. Sets today's ranked target country and clears today's ranked sessions so users can replay the new challenge.")
            .Produces<SetTodayRankedTargetResponse>()
            .ProducesValidationProblem()
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
}

public sealed record SetTodayRankedTargetRequest(string CountryId);

public sealed record SetTodayRankedTargetResponse(
    DateOnly ChallengeDateUtc,
    string TargetCountryId,
    string TargetCountryName,
    bool SessionsReset);

public sealed class SetTodayRankedTargetRequestValidator : AbstractValidator<SetTodayRankedTargetRequest>
{
    public SetTodayRankedTargetRequestValidator()
    {
        RuleFor(request => request.CountryId)
            .NotEmpty()
            .MaximumLength(16);
    }
}
