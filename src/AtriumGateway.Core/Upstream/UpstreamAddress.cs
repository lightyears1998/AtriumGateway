namespace AtriumGateway.Core.Upstream;

/// <summary>上游 baseUrl 的校验与规范化（纯函数）。</summary>
public static class UpstreamAddress
{
    /// <summary>要求绝对 http/https 地址，并保证以 <c>/</c> 结尾；失败时给出可诊断但不含密钥的原因。</summary>
    public static bool TryNormalize(string? configured, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(configured))
        {
            error = "上游地址为空";
            return false;
        }

        if (!Uri.TryCreate(configured.Trim(), UriKind.Absolute, out Uri? uri))
        {
            error = "上游地址不是绝对 URI";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "上游地址仅支持 http 或 https";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            error = "上游地址不得包含查询串或片段";
            return false;
        }

        string baseAddress = uri.GetLeftPart(UriPartial.Path);
        normalized = baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/";
        return true;
    }
}
