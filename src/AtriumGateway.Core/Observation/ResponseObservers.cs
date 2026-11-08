namespace AtriumGateway.Core.Observation;

/// <summary>SSE 逐行观察器：只读识别 <c>data:</c> 事件中的 <c>model</c>/<c>usage</c>。</summary>
internal sealed class SseUsageObserver : IResponseObserver
{
    private readonly int _maxBufferedBytes;

    public SseUsageObserver(int maxBufferedBytes) => _maxBufferedBytes = maxBufferedBytes;

    public void Observe(ReadOnlySpan<byte> chunk)
        => throw new NotImplementedException(); // TODO：增量 UTF-8 行解析，单行/单事件大小受 _maxBufferedBytes 限制

    public UsageSnapshot Complete()
        => throw new NotImplementedException(); // TODO
}

/// <summary>非流式 JSON 观察器：在大小上限内缓冲，结束后用 Utf8JsonReader 提取 <c>model</c>/<c>usage</c>。</summary>
internal sealed class JsonUsageObserver : IResponseObserver
{
    private readonly int _maxBufferedBytes;

    public JsonUsageObserver(int maxBufferedBytes) => _maxBufferedBytes = maxBufferedBytes;

    public void Observe(ReadOnlySpan<byte> chunk)
        => throw new NotImplementedException(); // TODO

    public UsageSnapshot Complete()
        => throw new NotImplementedException(); // TODO
}

/// <summary>压缩响应：不解压，直接标记为不可观测。</summary>
internal sealed class CompressedResponseObserver : IResponseObserver
{
    public void Observe(ReadOnlySpan<byte> chunk)
        => throw new NotImplementedException(); // TODO：忽略字节，不改动转发

    public UsageSnapshot Complete()
        => throw new NotImplementedException(); // TODO：CompressedResponse
}

/// <summary>无法识别的响应：不做观测。</summary>
internal sealed class UnsupportedResponseObserver : IResponseObserver
{
    public void Observe(ReadOnlySpan<byte> chunk)
        => throw new NotImplementedException(); // TODO：忽略字节

    public UsageSnapshot Complete()
        => throw new NotImplementedException(); // TODO：Unknown
}
