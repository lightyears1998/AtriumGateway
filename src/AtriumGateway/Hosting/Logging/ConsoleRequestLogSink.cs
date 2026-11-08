using AtriumGateway.Core.Logging;
using Microsoft.Extensions.Logging;

namespace AtriumGateway.Hosting.Logging;

/// <summary>元数据日志输出（console）；字段通过源生成写入，不记录正文与凭据。</summary>
public sealed partial class ConsoleRequestLogSink : IRequestLogSink
{
    private readonly ILogger<ConsoleRequestLogSink> _logger;

    public ConsoleRequestLogSink(ILogger<ConsoleRequestLogSink> logger) => _logger = logger;

    public ValueTask WriteAsync(GatewayRequestLog entry, CancellationToken cancellationToken)
    {
        LogRequest(
            _logger,
            entry.RequestId,
            entry.AccountId,
            entry.Method,
            entry.PathAndQuery,
            entry.StatusCode,
            (long)entry.Elapsed.TotalMilliseconds,
            entry.UpstreamId,
            entry.AttemptedUpstreams.Count,
            entry.SwitchReasons.Count,
            entry.Usage.Model,
            entry.Usage.Usage.PromptTokens,
            entry.Usage.Usage.CompletionTokens,
            entry.Usage.Usage.TotalTokens,
            (int)entry.Outcome,
            entry.FailureDetail);

        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "request {RequestId} account={AccountId} {Method} {PathAndQuery} status={StatusCode} elapsedMs={ElapsedMs} upstream={UpstreamId} attempts={AttemptCount} switches={SwitchCount} model={Model} prompt={PromptTokens} completion={CompletionTokens} total={TotalTokens} outcome={Outcome} detail={FailureDetail}")]
    private static partial void LogRequest(
        ILogger logger,
        string requestId,
        string? accountId,
        string method,
        string pathAndQuery,
        int statusCode,
        long elapsedMs,
        string? upstreamId,
        int attemptCount,
        int switchCount,
        string? model,
        int? promptTokens,
        int? completionTokens,
        int? totalTokens,
        int outcome,
        string? failureDetail);
}
