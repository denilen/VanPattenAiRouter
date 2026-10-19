namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents batch processing metrics.
/// </summary>
public class BatchMetrics
{
    /// <summary>
    ///     The total number of requests in the batch.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    ///     The number of completed requests.
    /// </summary>
    public int CompletedRequests { get; set; }

    /// <summary>
    ///     The number of failed requests.
    /// </summary>
    public int FailedRequests { get; set; }
}