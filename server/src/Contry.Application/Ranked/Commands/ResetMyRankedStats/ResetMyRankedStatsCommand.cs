namespace Contry.Application.Ranked;

public sealed record ResetMyRankedStatsCommand(Guid UserId);

public sealed class ResetMyRankedStatsCommandHandler(IRankedStore rankedStore)
{
    private readonly IRankedStore _rankedStore = rankedStore;

    public async Task HandleAsync(ResetMyRankedStatsCommand command, CancellationToken cancellationToken)
    {
        await _rankedStore.DeleteAllUserDataAsync(command.UserId, cancellationToken);
    }
}
