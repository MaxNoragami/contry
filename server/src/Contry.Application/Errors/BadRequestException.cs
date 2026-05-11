namespace Contry.Application.Errors;

public abstract class BadRequestException(string type, string title, string detail) : AppException(type, title, 400, detail);
