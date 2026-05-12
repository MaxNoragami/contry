namespace Contry.Domain.TestRecords;

public sealed class TestRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
