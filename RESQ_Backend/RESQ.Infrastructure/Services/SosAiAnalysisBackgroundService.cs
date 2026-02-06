using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services;

/// <summary>
/// Extended queue interface for background service
/// </summary>
public interface ISosAiAnalysisQueueInternal : ISosAiAnalysisQueue
{
    ValueTask<SosAiAnalysisTask> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Channel-based queue implementation
/// </summary>
public class SosAiAnalysisQueue : ISosAiAnalysisQueueInternal
{
    private readonly Channel<SosAiAnalysisTask> _queue;

    public SosAiAnalysisQueue()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<SosAiAnalysisTask>(options);
    }

    public async ValueTask QueueAsync(SosAiAnalysisTask task)
    {
        await _queue.Writer.WriteAsync(task);
    }

    public async ValueTask<SosAiAnalysisTask> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

/// <summary>
/// Background service that processes AI analysis tasks
/// </summary>
public class SosAiAnalysisBackgroundService : BackgroundService
{
    private readonly ISosAiAnalysisQueueInternal _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SosAiAnalysisBackgroundService> _logger;

    public SosAiAnalysisBackgroundService(
        ISosAiAnalysisQueueInternal queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SosAiAnalysisBackgroundService> logger)
    {
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SOS AI Analysis Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation("Processing AI analysis task for SOS Request Id={sosRequestId}", task.SosRequestId);

                using var scope = _serviceScopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<ISosAiAnalysisService>();

                await analysisService.AnalyzeAndSaveAsync(
                    task.SosRequestId,
                    task.StructuredData,
                    task.RawMessage,
                    task.SosType,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI analysis task");
            }
        }

        _logger.LogInformation("SOS AI Analysis Background Service is stopping.");
    }
}
