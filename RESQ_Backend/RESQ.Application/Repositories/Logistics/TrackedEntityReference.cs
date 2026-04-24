namespace RESQ.Application.Repositories.Logistics;

public sealed class TrackedEntityReference<TModel>
{
    private readonly Func<int> _currentIdAccessor;

    public TrackedEntityReference(TModel snapshot, Func<int> currentIdAccessor)
    {
        Snapshot = snapshot;
        _currentIdAccessor = currentIdAccessor;
    }

    public TModel Snapshot { get; }

    public int CurrentId => _currentIdAccessor();
}
