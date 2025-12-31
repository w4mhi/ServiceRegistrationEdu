using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Services.HealthInsights;

/// <summary>
/// Request for health analysis
/// </summary>
public class AnalysisRequest
{
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public Guid ServiceId { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public bool IsManualTrigger { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 0; // Higher = more important
}

/// <summary>
/// Thread-safe queue for analysis requests with priority support
/// </summary>
public class AnalysisQueueService
{
    private readonly Channel<AnalysisRequest> channel;
    private readonly ConcurrentDictionary<Guid, AnalysisRequest> pendingRequests;
    private readonly ILogger<AnalysisQueueService>? logger;

    public AnalysisQueueService(ILogger<AnalysisQueueService>? logger = null)
    {
        this.logger = logger;
        
        BoundedChannelOptions options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        
        channel = Channel.CreateBounded<AnalysisRequest>(options);
        pendingRequests = new ConcurrentDictionary<Guid, AnalysisRequest>();
    }

    /// <summary>
    /// Add analysis request to queue
    /// </summary>
    public async Task EnqueueAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isDuplicate = pendingRequests.Values
            .Any(r => r.ServiceId == request.ServiceId 
                && (DateTime.UtcNow - r.QueuedAt).TotalMinutes < 5);

        if (isDuplicate)
        {
            logger?.LogWarning("Duplicate analysis request for service {ServiceId} within 5 minutes, skipping", 
                request.ServiceId);
            return;
        }

        pendingRequests.TryAdd(request.RequestId, request);
        
        await channel.Writer.WriteAsync(request, cancellationToken);
        
        logger?.LogInformation("Queued analysis request {RequestId} for service {ServiceId}, priority={Priority}, queue depth={Depth}", 
            request.RequestId, request.ServiceId, request.Priority, GetQueueDepth());
    }

    /// <summary>
    /// Dequeue next analysis request (blocks until available)
    /// </summary>
    public async Task<AnalysisRequest?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AnalysisRequest request = await channel.Reader.ReadAsync(cancellationToken);
            
            pendingRequests.TryRemove(request.RequestId, out AnalysisRequest? removed);
            
            logger?.LogDebug("Dequeued analysis request {RequestId} for service {ServiceId}", 
                request.RequestId, request.ServiceId);
            
            return request;
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("Dequeue operation cancelled");
            return null;
        }
    }

    /// <summary>
    /// Get current queue depth
    /// </summary>
    public int GetQueueDepth()
    {
        return pendingRequests.Count;
    }

    /// <summary>
    /// Check if queue has capacity
    /// </summary>
    public bool HasCapacity()
    {
        return pendingRequests.Count < 1000;
    }

    /// <summary>
    /// Complete the channel (no more writes)
    /// </summary>
    public void Complete()
    {
        channel.Writer.Complete();
        logger?.LogInformation("Analysis queue completed, pending={Count}", pendingRequests.Count);
    }
}
