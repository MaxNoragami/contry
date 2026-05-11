namespace Contry.Application.Errors;

public abstract class AppException(string type, string title, int status, string detail) : Exception(detail)
{
    public string Type { get; } = type;

    public string Title { get; } = title;

    public int Status { get; } = status;

    public string Detail { get; } = detail;

    public virtual IDictionary<string, object?> GetExtensions() => new Dictionary<string, object?>();
}
