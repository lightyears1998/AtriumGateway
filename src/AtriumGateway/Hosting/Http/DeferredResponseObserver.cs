using AtriumGateway.Core.Observation;
using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Http;

/// <summary>
/// 延迟到首次写入时创建真实观察器的包装：此时响应头已由转发层复制完成，
/// 才能依据 <c>Content-Type</c> 与 <c>Content-Encoding</c> 选择观察器。
/// </summary>
internal sealed class DeferredResponseObserver : IResponseObserver
{
    private readonly HttpContext _context;
    private readonly IResponseObserverFactory _factory;
    private IResponseObserver? _observer;

    public DeferredResponseObserver(HttpContext context, IResponseObserverFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    public void Observe(ReadOnlySpan<byte> chunk)
    {
        IResponseObserver observer = _observer ??= CreateObserver();
        observer.Observe(chunk);
    }

    public UsageSnapshot Complete()
        => _observer is null ? UsageSnapshot.Unknown : _observer.Complete();

    private IResponseObserver CreateObserver()
    {
        string? contentType = _context.Response.ContentType;
        string contentEncoding = _context.Response.Headers.ContentEncoding.ToString();
        bool isStreaming = contentType is not null
            && contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

        ResponseObservationContext observationContext = new(
            contentType,
            string.IsNullOrEmpty(contentEncoding) ? null : contentEncoding,
            isStreaming,
            ResponseObservationContext.DefaultMaxBufferedBytes);

        return _factory.Create(observationContext);
    }
}
