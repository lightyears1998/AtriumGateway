using AtriumGateway.Core.Logging;
using Microsoft.Extensions.Logging;

namespace AtriumGateway.Hosting.Logging;

/// <summary>元数据日志输出（console）；结构化字段通过 <c>[LoggerMessage]</c> 生成。</summary>
public sealed class ConsoleRequestLogSink : IRequestLogSink
{
    private readonly ILogger<ConsoleRequestLogSink> _logger;

    public ConsoleRequestLogSink(ILogger<ConsoleRequestLogSink> logger) => _logger = logger;

    public ValueTask WriteAsync(GatewayRequestLog entry, CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：以 [LoggerMessage] 输出 entry 的元数据字段（含 usage/outcome）
}
