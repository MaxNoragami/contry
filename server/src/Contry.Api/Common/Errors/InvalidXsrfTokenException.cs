using Contry.Application.Errors;

namespace Contry.Api.Common.Errors;

public sealed class InvalidXsrfTokenException() : BadRequestException(
    "/problems/security/invalid-xsrf-token",
    "Invalid XSRF token.",
    "The provided X-XSRF-TOKEN value is invalid, expired, or does not match the current access-token identity.");
