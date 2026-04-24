namespace RESQ.Application.Repositories.Finance;

public sealed class TrackedFinanceEntityReference<TModel>
{
    private readonly Func<int> _currentIdAccessor;

    public TrackedFinanceEntityReference(TModel snapshot, Func<int> currentIdAccessor)
    {
        Snapshot = snapshot;
        _currentIdAccessor = currentIdAccessor;
    }

    public TModel Snapshot { get; }

    public int CurrentId => _currentIdAccessor();
}
