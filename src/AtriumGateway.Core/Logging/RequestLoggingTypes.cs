using AtriumGateway.Core.Observation;
using AtriumGateway.Core.Upstream;

namespace AtriumGateway.Core.Logging;

/// <summary>请求结果分类（用于日志与统计）。</summary>
public enum RequestOutcome
{
    Succeeded = 0,
    UpstreamError = 1,
    GatewayError = 2,
    ClientDisconnected = 3,
}

/// <summary>一次请求的元数据日志；不包含任何正文或凭据。</summary>
public sealed record GatewayRequestLog
{
    public required string RequestId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Method { get; init; }

    public required string PathAndQuery { get; init; }

    public int StatusCode { get; init; }

    public TimeSpan Elapsed { get; init; }

    public string? AccountId { get; init; }

    /// <summary>最终处理该请求的上游标识。</summary>
    public string? UpstreamId { get; init; }

    /// <summary>按尝试顺序记录的上游标识，用于还原换发路径。</summary>
    public IReadOnlyList<string> AttemptedUpstreams { get; init; } = Array.Empty<string>();

    /// <summary>按发生顺序记录的换发原因；与 <see cref="AttemptedUpstreams"/> 除最后一项外一一对应。</summary>
    public IReadOnlyList<UpstreamSwitchReason> SwitchReasons { get; init; } = Array.Empty<UpstreamSwitchReason>();

    public UsageSnapshot Usage { get; init; } = UsageSnapshot.Unknown;

    public bool IsStreaming { get; init; }

    public long? RequestBytes { get; init; }

    public long? ResponseBytes { get; init; }

    public RequestOutcome Outcome { get; init; }

    /// <summary>失败或异常的可诊断说明；写入前须经脱敏，不得包含密钥与正文。</summary>
    public string? FailureDetail { get; init; }
}

/// <summary>元数据日志输出边界；实现不得抛出影响转发的异常。</summary>
public interface IRequestLogSink
{
    ValueTask WriteAsync(GatewayRequestLog entry, CancellationToken cancellationToken);
}

/// <summary>依据状态码与失败来源归类请求结果。</summary>
public static class RequestOutcomeClassifier
{
    public static RequestOutcome Classify(int statusCode, bool clientDisconnected, bool gatewayError)
    {
        if (clientDisconnected)
        {
            return RequestOutcome.ClientDisconnected;
        }

        if (gatewayError)
        {
            return RequestOutcome.GatewayError;
        }

        return statusCode is >= 200 and < 300 ? RequestOutcome.Succeeded : RequestOutcome.UpstreamError;
    }
}
