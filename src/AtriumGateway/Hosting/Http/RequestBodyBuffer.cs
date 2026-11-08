using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Http;

/// <summary>
/// 请求体可重放缓冲（换发前置条件）。
/// 三种形态：空体（可重放）；声明长度超上限（不缓冲、不可重放）；上限内已缓冲（可重放、可重置游标）。
/// </summary>
public sealed class RequestBodyBuffer : IDisposable
{
    private readonly HttpContext _context;

    internal RequestBodyBuffer(HttpContext context, bool isReplayable)
    {
        _context = context;
        IsReplayable = isReplayable;
    }

    /// <summary>本次请求的请求体能否为下一次尝试重放。</summary>
    public bool IsReplayable { get; }

    /// <summary>下一次尝试前把请求体游标重置到开头；不可重放时为空操作。</summary>
    public void Rewind()
        => throw new NotImplementedException(); // TODO：缓冲流 Position = 0 / 重置读取状态

    /// <summary>恢复原始请求体并释放缓冲内存；由处理器在请求结束时调用一次。</summary>
    public void Dispose()
        => throw new NotImplementedException(); // TODO：恢复原始 Request.Body 并释放缓冲内存
}

/// <summary>创建请求体缓冲。</summary>
public sealed class RequestBodyBufferFactory
{
    private readonly long _limitBytes;

    public RequestBodyBufferFactory(long limitBytes) => _limitBytes = limitBytes;

    public RequestBodyBuffer Create(HttpContext context)
    {
        if (IsEmptyBody(context))
        {
            return new RequestBodyBuffer(context, isReplayable: true);
        }

        if (DeclaredLengthExceedsLimit(context))
        {
            return new RequestBodyBuffer(context, isReplayable: false);
        }

        return BufferWithinLimit(context);
    }

    private static bool IsEmptyBody(HttpContext context)
        => throw new NotImplementedException(); // TODO：无 Content-Length 或为 0，且非分块传输

    private bool DeclaredLengthExceedsLimit(HttpContext context)
        => throw new NotImplementedException(); // TODO：Content-Length 超过 _limitBytes

    private RequestBodyBuffer BufferWithinLimit(HttpContext context)
        => throw new NotImplementedException(); // TODO：限长读入内存并接管 Request.Body；实际超限则不可重放并保留续读能力
}
