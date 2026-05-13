namespace Contry.Domain.Datasets;

public sealed class BuiltInDatasetDocument
{
    public string Path { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Checksum { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
