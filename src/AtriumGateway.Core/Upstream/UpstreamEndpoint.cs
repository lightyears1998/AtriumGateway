namespace AtriumGateway.Core.Upstream;

/// <summary>一个上游的运行时表示；ApiKey 永不输出到日志或错误响应。</summary>
public sealed record UpstreamEndpoint(string Id, string BaseAddress, string ApiKey);
