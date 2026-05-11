namespace Contry.Application.Errors;

public abstract class ConflictException(string type, string title, string detail) : AppException(type, title, 409, detail);
