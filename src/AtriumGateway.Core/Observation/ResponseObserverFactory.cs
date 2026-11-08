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
        => throw new NotImplementedException(); // TODO：依据 Content-Encoding 判断

    private static bool IsServerSentEvents(ResponseObservationContext context)
        => throw new NotImplementedException(); // TODO：依据 Content-Type 判断

    private static bool IsJson(ResponseObservationContext context)
        => throw new NotImplementedException(); // TODO：依据 Content-Type 判断
}
