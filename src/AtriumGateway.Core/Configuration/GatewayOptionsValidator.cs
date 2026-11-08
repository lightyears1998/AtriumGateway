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

    private static void CollectAccountIssues(GatewayOptions options, List<ConfigurationIssue> issues)
    {
        if (options.Accounts.Count == 0)
        {
            issues.Add(new ConfigurationIssue("account.empty", "未配置任何账号"));
            return;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (AccountSettings account in options.Accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Id))
            {
                issues.Add(new ConfigurationIssue("account.id.empty", "账号标识为空"));
            }
            else if (!ids.Add(account.Id))
            {
                issues.Add(new ConfigurationIssue("account.id.duplicated", $"账号标识重复：{account.Id}"));
            }

            if (string.IsNullOrEmpty(account.Key))
            {
                issues.Add(new ConfigurationIssue("account.key.empty", $"账号 {account.Id} 的密钥为空"));
            }
            else if (!keys.Add(account.Key))
            {
                issues.Add(new ConfigurationIssue("account.key.duplicated", $"账号 {account.Id} 的密钥与其他账号重复"));
            }
        }
    }

    private static void CollectUpstreamIssues(GatewayOptions options, List<ConfigurationIssue> issues)
    {
        if (options.Upstreams.Count == 0)
        {
            issues.Add(new ConfigurationIssue("upstream.empty", "未配置任何上游"));
            return;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (UpstreamSettings upstream in options.Upstreams)
        {
            if (string.IsNullOrWhiteSpace(upstream.Id))
            {
                issues.Add(new ConfigurationIssue("upstream.id.empty", "上游标识为空"));
            }
            else if (!ids.Add(upstream.Id))
            {
                issues.Add(new ConfigurationIssue("upstream.id.duplicated", $"上游标识重复：{upstream.Id}"));
            }

            if (string.IsNullOrEmpty(upstream.ApiKey))
            {
                issues.Add(new ConfigurationIssue("upstream.apiKey.empty", $"上游 {upstream.Id} 的密钥为空"));
            }

            if (!UpstreamAddress.TryNormalize(upstream.BaseAddress, out _, out string? error))
            {
                issues.Add(new ConfigurationIssue("upstream.baseAddress", error ?? "上游地址非法"));
            }
        }
    }

    private static void CollectLimitIssues(GatewayOptions options, List<ConfigurationIssue> issues)
    {
        if (options.UpstreamActivityTimeout <= TimeSpan.Zero)
        {
            issues.Add(new ConfigurationIssue("limit.activityTimeout", "上游空闲超时须为正值"));
        }

        if (options.UpstreamCooldown <= TimeSpan.Zero)
        {
            issues.Add(new ConfigurationIssue("limit.cooldown", "上游冷却期须为正值"));
        }

        if (options.RequestBodyBufferLimit <= 0)
        {
            issues.Add(new ConfigurationIssue("limit.requestBodyBuffer", "请求体缓冲上限须为正值"));
        }
    }
}
