namespace AtriumGateway.Core.Observation;

/// <summary>按响应特征选择具体观察器。</summary>
public sealed class ResponseObserverFactory : IResponseObserverFactory
{
    public IResponseObserver Create(ResponseObservationContext context)
    {
        if (IsCompressed(context))
        {
            return new CompressedResponseObserver();
        }

        if (IsServerSentEvents(context))
        {
            return new SseUsageObserver(context.MaxBufferedBytes);
        }

        if (IsJson(context))
        {
            return new JsonUsageObserver(context.MaxBufferedBytes);
        }

        return new UnsupportedResponseObserver();
    }

    private static bool IsCompressed(ResponseObservationContext context)
        => !string.IsNullOrEmpty(context.ContentEncoding)
            && !string.Equals(context.ContentEncoding, "identity", StringComparison.OrdinalIgnoreCase);

    private static bool IsServerSentEvents(ResponseObservationContext context)
        => context.ContentType is not null
            && context.ContentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

    private static bool IsJson(ResponseObservationContext context)
    {
        if (context.ContentType is null)
        {
            return false;
        }

        return context.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || context.ContentType.Contains("+json", StringComparison.OrdinalIgnoreCase);
    }
}
