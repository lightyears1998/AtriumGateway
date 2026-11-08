namespace AtriumGateway.Core.Upstream;

/// <summary>上游 baseUrl 的校验与规范化（纯函数）。</summary>
public static class UpstreamAddress
{
    /// <summary>要求绝对 http/https 地址，并保证以 <c>/</c> 结尾；失败时给出可诊断但不含密钥的原因。</summary>
    public static bool TryNormalize(string? configured, out string? normalized, out string? error)
        => throw new NotImplementedException(); // TODO
}
