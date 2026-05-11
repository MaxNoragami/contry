using Contry.Application.Errors;

namespace Contry.Api.Common.Errors;

public sealed class MissingXsrfTokenException() : BadRequestException(
    "/problems/security/missing-xsrf-token",
    "Missing XSRF token.",
    "The X-XSRF-TOKEN header is required for this request.");
