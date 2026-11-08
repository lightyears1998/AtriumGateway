using AtriumGateway.Core.Observation;
using AtriumGateway.Core.Upstream;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AtriumGateway.Hosting.Http;

/// <summary>
/// 在转发期间把响应体替换为只读观察用的 tee 包装，结束时恢复原始响应体特性。
/// 观察不得改变字节、顺序或时序；异常一律降级为 unknown。
/// </summary>
public sealed class ResponseObservationScope : IDisposable
{
    private readonly HttpContext _context;
    private readonly IHttpResponseBodyFeature? _originalFeature;
    private readonly IResponseObserver _observer;
    private bool _completed;

    private ResponseObservationScope(HttpContext context, IHttpResponseBodyFeature? originalFeature, IResponseObserver observer)
    {
        _context = context;
        _originalFeature = originalFeature;
        _observer = observer;
    }

    public static ResponseObservationScope Begin(HttpContext context, IResponseObserverFactory observerFactory)
    {
        ResponseObservationContext observationContext = CreateObservationContext(context);
        IResponseObserver observer = observerFactory.Create(observationContext);
        IHttpResponseBodyFeature originalFeature = GetResponseBodyFeature(context);
        context.Features.Set<IHttpResponseBodyFeature>(new ResponseTeeFeature(originalFeature, observer));
        return new ResponseObservationScope(context, originalFeature, observer);
    }

    /// <summary>
    /// 结束观察并返回结果，只在请求终结路径调用一次。
    /// 换发路径不调用本方法，该次观察的数据随即丢弃；重复调用返回首次结果。
    /// 观察过程出错时降级为 <see cref="UsageSnapshot.Unknown"/>，不向调用方抛出。
    /// </summary>
    public UsageSnapshot Complete()
    {
        if (_completed)
        {
            return UsageSnapshot.Unknown;
        }

        _completed = true;
        try
        {
            return _observer.Complete();
        }
        catch (Exception)
        {
            return UsageSnapshot.Unknown;
        }
    }

    /// <summary>恢复原始响应体特性；可在 <see cref="Complete"/> 之前或之后调用，重复调用无副作用。</summary>
    public void Dispose() => RestoreOriginalFeature(_context, _originalFeature);

    private static void RestoreOriginalFeature(HttpContext context, IHttpResponseBodyFeature? originalFeature)
        => throw new NotImplementedException(); // TODO：把原始特性写回 context.Features；无原始特性时移除包装

    private static ResponseObservationContext CreateObservationContext(HttpContext context)
        => throw new NotImplementedException(); // TODO：读取响应头得到 Content-Type/Content-Encoding/是否流式，并带上 ResponseObservationContext.DefaultMaxBufferedBytes

    private static IHttpResponseBodyFeature GetResponseBodyFeature(HttpContext context)
        => throw new NotImplementedException(); // TODO
}
