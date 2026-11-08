namespace AtriumGateway.Core.Upstream;

/// <summary>触发换发的原因：上游状态码与错误码（错误码可能为空）。</summary>
public sealed record UpstreamSwitchReason(int StatusCode, string? ErrorCode);
