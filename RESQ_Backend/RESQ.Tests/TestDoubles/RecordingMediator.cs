using MediatR;

namespace RESQ.Tests.TestDoubles;

internal sealed class RecordingMediator(Func<object, object?>? sendHandler = null) : IMediator
{
    private readonly Func<object, object?> _sendHandler = sendHandler ?? (_ => null);

    public List<object> SentRequests { get; } = [];

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);

        var result = _sendHandler(request);
        if (result is Exception exception)
            throw exception;

        return Task.FromResult(result is null ? default! : (TResponse)result);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);

        var result = _sendHandler(request);
        if (result is Exception exception)
            throw exception;

        return Task.FromResult(result);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        SentRequests.Add(request!);

        var result = _sendHandler(request!);
        if (result is Exception exception)
            throw exception;

        return Task.CompletedTask;
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => EmptyStream<TResponse>();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => EmptyStream<object?>();

    private static async IAsyncEnumerable<T> EmptyStream<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
