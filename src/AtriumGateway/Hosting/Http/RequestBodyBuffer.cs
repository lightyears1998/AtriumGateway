using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Http;

/// <summary>
/// 请求体可重放缓冲（换发前置条件）。
/// 两种形态：空体（可重放、无需缓冲）；上限内已缓冲（可重放、可重置游标）；其余不可重放。
/// </summary>
public sealed class RequestBodyBuffer : IDisposable
{
    private readonly HttpContext _context;
    private readonly Stream? _originalBody;
    private readonly MemoryStream? _buffered;
    private readonly bool _replayable;

    internal RequestBodyBuffer(HttpContext context, Stream? originalBody, MemoryStream? buffered, bool replayable)
    {
        _context = context;
        _originalBody = originalBody;
        _buffered = buffered;
        _replayable = replayable;
    }

    /// <summary>本次请求的请求体能否为下一次尝试重放。</summary>
    public bool IsReplayable => _replayable;

    /// <summary>下一次尝试前把请求体游标重置到开头；没有缓冲时为空操作。</summary>
    public void Rewind()
    {
        if (_buffered is null)
        {
            return;
        }

        _buffered.Position = 0;
        _context.Request.Body = _buffered;
    }

    /// <summary>恢复原始请求体并释放缓冲内存。</summary>
    public void Dispose()
    {
        if (_originalBody is not null)
        {
            _context.Request.Body = _originalBody;
        }

        _buffered?.Dispose();
    }
}

/// <summary>创建请求体缓冲。</summary>
public sealed class RequestBodyBufferFactory
{
    private readonly long _limitBytes;

    public RequestBodyBufferFactory(long limitBytes) => _limitBytes = limitBytes;

    public async ValueTask<RequestBodyBuffer> CreateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (IsEmptyBody(context))
        {
            return new RequestBodyBuffer(context, originalBody: null, buffered: null, replayable: true);
        }

        if (context.Request.ContentLength is not long contentLength || contentLength > _limitBytes)
        {
            return new RequestBodyBuffer(context, originalBody: null, buffered: null, replayable: false);
        }

        MemoryStream buffered = new(capacity: (int)contentLength);
        await context.Request.Body.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        Stream originalBody = context.Request.Body;
        context.Request.Body = buffered;
        return new RequestBodyBuffer(context, originalBody, buffered, replayable: true);
    }

    private static bool IsEmptyBody(HttpContext context)
    {
        if (context.Request.ContentLength is 0)
        {
            return true;
        }

        if (context.Request.ContentLength is > 0)
        {
            return false;
        }

        return !MayHaveBody(context.Request.Method);
    }

    private static bool MayHaveBody(string method)
        => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);
}
