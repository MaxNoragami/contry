namespace Contry.Application.Errors;

public abstract class UnauthorizedException(string type, string title, string detail) : AppException(type, title, 401, detail);
