namespace InvoiceBilling.Infrastructure.Standalone;

/// <summary>
/// Determines which infrastructure backing services are wired into the DI container.
/// Configured via <c>Infrastructure:Mode</c> in appsettings or environment variables.
/// </summary>
public enum InfrastructureMode
{
    /// <summary>
    /// Production / learning path. Requires Docker + LocalStack (or real AWS).
    /// Uses SQS for job queuing and S3 for PDF storage.
    /// </summary>
    Cloud = 0,

    /// <summary>
    /// Zero-dependency local deployment.
    /// Uses an in-process <see cref="System.Threading.Channels.Channel{T}"/> for job
    /// queuing and the local filesystem for PDF storage.
    /// </summary>
    Standalone = 1
}
