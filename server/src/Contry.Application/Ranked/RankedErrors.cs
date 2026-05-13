using Contry.Application.Errors;

namespace Contry.Application.Ranked;

public sealed class RankedInvalidCountryException() : BadRequestException(
    "/problems/ranked/invalid-country",
    "Invalid ranked guess country.",
    "The submitted country id does not belong to the official ranked country pool.");

public sealed class RankedDuplicateGuessException() : ConflictException(
    "/problems/ranked/duplicate-guess",
    "Duplicate ranked guess.",
    "This country was already guessed in the current ranked session.");

public sealed class RankedSessionCompletedException() : ConflictException(
    "/problems/ranked/session-completed",
    "Ranked session already completed.",
    "The current ranked session is already completed and cannot accept more guesses.");
