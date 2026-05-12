using Contry.Domain.TestRecords;

namespace Contry.Api.Features.TestRecords;

public sealed record TestRecordResponse(Guid Id, Guid UserId, string Name, string Notes, DateTimeOffset CreatedAtUtc)
{
    public static TestRecordResponse FromEntity(TestRecord record)
        => new(record.Id, record.UserId, record.Name, record.Notes, record.CreatedAtUtc);
}
