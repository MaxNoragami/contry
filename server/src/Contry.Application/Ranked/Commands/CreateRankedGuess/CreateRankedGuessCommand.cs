namespace Contry.Application.Ranked;

public sealed record CreateRankedGuessCommand(Guid UserId, string CountryId);
