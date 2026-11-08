using AtriumGateway.Core.Upstream;

namespace AtriumGateway.Core.Configuration;

/// <summary>单条配置问题；<see cref="Message"/> 不得包含密钥等敏感值。</summary>
public sealed record ConfigurationIssue(string Code, string Message);

/// <summary>配置校验结果。</summary>
public sealed record ConfigurationValidationResult(IReadOnlyList<ConfigurationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;

    public static ConfigurationValidationResult Valid { get; } = new(Array.Empty<ConfigurationIssue>());
}

/// <summary>启动期配置校验：只汇集问题清单，不抛异常，由宿主决定失败方式。</summary>
public static class GatewayOptionsValidator
{
    public static ConfigurationValidationResult Validate(GatewayOptions options)
    {
        List<ConfigurationIssue> issues = new();
        CollectAccountIssues(options, issues);
        CollectUpstreamIssues(options, issues);
        CollectLimitIssues(options, issues);
        return new ConfigurationValidationResult(issues);
    }

    // 账号：非空；Id 与 Key 非空且互不重复。
    private static void CollectAccountIssues(GatewayOptions options, List<ConfigurationIssue> issues)
        => throw new NotImplementedException(); // TODO

    // 上游：非空；Id 非空且互不重复；BaseAddress 须能规范化为绝对 http/https 地址。
    private static void CollectUpstreamIssues(GatewayOptions options, List<ConfigurationIssue> issues)
    {
        if (options.Upstreams.Count == 0)
        {
            issues.Add(new ConfigurationIssue("upstream.empty", "未配置任何上游"));
            return;
        }

        foreach (UpstreamSettings upstream in options.Upstreams)
        {
            if (!UpstreamAddress.TryNormalize(upstream.BaseAddress, out _, out string? error))
            {
                issues.Add(new ConfigurationIssue("upstream.baseAddress", error ?? "上游地址非法"));
            }
        }
    }

    // 数值参数：空闲超时与冷却期为正值，请求体缓冲上限为正值。
    private static void CollectLimitIssues(GatewayOptions options, List<ConfigurationIssue> issues)
        => throw new NotImplementedException(); // TODO
}
