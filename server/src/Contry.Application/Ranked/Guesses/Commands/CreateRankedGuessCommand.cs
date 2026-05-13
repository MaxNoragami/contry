namespace Contry.Application.Ranked.Guesses.Commands;

public sealed record CreateRankedGuessCommand(Guid UserId, string CountryId);
