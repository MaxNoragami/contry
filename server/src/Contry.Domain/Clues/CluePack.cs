namespace Contry.Domain.Clues;

public sealed class CluePack
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string DatasetId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Comparator { get; set; } = string.Empty;

    public string? UnitSymbol { get; set; }

    public string Icon { get; set; } = string.Empty;

    public string? CategoriesJson { get; set; }

    public string RowsJson { get; set; } = string.Empty;

    public string Visibility { get; set; } = "public";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
