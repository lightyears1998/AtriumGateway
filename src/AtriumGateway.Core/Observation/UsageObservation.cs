namespace AtriumGateway.Core.Observation;

/// <summary>usage 观测结果的可信度。</summary>
public enum UsageAvailability
{
    Unknown = 0,
    Parsed = 1,
    UpstreamNotReported = 2,
    CompressedResponse = 3,
    ObservationAborted = 4,
}

/// <summary>上游返回的 token 用量；不可得时各字段为 null。</summary>
public sealed record TokenUsage(int? PromptTokens, int? CompletionTokens, int? TotalTokens)
{
    public static TokenUsage Empty { get; } = new(null, null, null);
}

/// <summary>一次响应观测的最终结果；<see cref="Model"/> 同样来自只读观测。</summary>
public sealed record UsageSnapshot(string? Model, TokenUsage Usage, UsageAvailability Availability)
{
    public static UsageSnapshot Unknown { get; } = new(null, TokenUsage.Empty, UsageAvailability.Unknown);
}

/// <summary>
/// 开始观察前可获知的响应特征与观察预算；只从响应头读取，不触碰正文。
/// </summary>
public sealed record ResponseObservationContext(
    string? ContentType,
    string? ContentEncoding,
    bool IsStreaming,
    int MaxBufferedBytes)
{
    /// <summary>观察器允许缓冲的默认上限（256 KiB）。</summary>
    public const int DefaultMaxBufferedBytes = 256 * 1024;
}

/// <summary>响应正文的只读观察器；不得改变转发字节、顺序或时序。</summary>
public interface IResponseObserver
{
    /// <summary>喂入一段已转发字节。实现不得修改字节、不得阻塞调用方，也不得向调用方抛出异常。</summary>
    void Observe(ReadOnlySpan<byte> chunk);

    /// <summary>结束观察并给出快照，只调用一次；数据不足或未识别时返回 unknown 语义。</summary>
    UsageSnapshot Complete();
}

/// <summary>按响应特征选择观察器。</summary>
public interface IResponseObserverFactory
{
    /// <summary>创建观察器；缓冲预算取自 <paramref name="context"/>。</summary>
    IResponseObserver Create(ResponseObservationContext context);
}
