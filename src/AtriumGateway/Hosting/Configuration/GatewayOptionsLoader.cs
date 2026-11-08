using System.Globalization;
using AtriumGateway.Core.Configuration;
using AtriumGateway.Core.Upstream;
using Microsoft.Extensions.Configuration;

namespace AtriumGateway.Hosting.Configuration;

/// <summary>
/// 手写配置绑定（零反射，AOT 安全）。
/// 键名与形状见 <c>appsettings.json.example</c>；真实配置不入库。
/// </summary>
public static class GatewayOptionsLoader
{
    public const string AccountsKey = "Gateway:Accounts";
    public const string UpstreamsKey = "Gateway:Upstreams";
    public const string UserAgentKey = "Gateway:UserAgent";
    public const string UpstreamActivityTimeoutKey = "Gateway:UpstreamActivityTimeout";
    public const string UpstreamCooldownKey = "Gateway:UpstreamCooldown";
    public const string RequestBodyBufferLimitKey = "Gateway:RequestBodyBufferLimit";

    /// <summary>读取并校验配置；校验不通过时快速失败，错误信息不含密钥。</summary>
    public static GatewayOptions Load(IConfiguration configuration)
    {
        GatewayOptions options = new()
        {
            Accounts = ReadAccountList(configuration, AccountsKey),
            Upstreams = NormalizeUpstreams(ReadUpstreamList(configuration, UpstreamsKey)),
            UserAgent = ReadOptionalString(configuration, UserAgentKey),
            UpstreamActivityTimeout = ReadTimeSpan(configuration, UpstreamActivityTimeoutKey, TimeSpan.FromMinutes(10)),
            UpstreamCooldown = ReadTimeSpan(configuration, UpstreamCooldownKey, TimeSpan.FromMinutes(5)),
            RequestBodyBufferLimit = ReadLong(configuration, RequestBodyBufferLimitKey, 4L * 1024 * 1024),
        };

        ConfigurationValidationResult validation = GatewayOptionsValidator.Validate(options);
        ThrowIfInvalid(validation);
        return options;
    }

    private static IReadOnlyList<AccountSettings> ReadAccountList(IConfiguration configuration, string key)
    {
        List<AccountSettings> accounts = new();
        foreach (IConfigurationSection section in configuration.GetSection(key).GetChildren())
        {
            accounts.Add(new AccountSettings(
                section["Id"] ?? string.Empty,
                section["Key"] ?? string.Empty));
        }

        return accounts;
    }

    private static IReadOnlyList<UpstreamSettings> ReadUpstreamList(IConfiguration configuration, string key)
    {
        List<UpstreamSettings> upstreams = new();
        foreach (IConfigurationSection section in configuration.GetSection(key).GetChildren())
        {
            upstreams.Add(new UpstreamSettings(
                section["Id"] ?? string.Empty,
                section["BaseAddress"] ?? string.Empty,
                section["ApiKey"] ?? string.Empty));
        }

        return upstreams;
    }

    private static IReadOnlyList<UpstreamSettings> NormalizeUpstreams(IReadOnlyList<UpstreamSettings> upstreams)
    {
        List<UpstreamSettings> normalized = new(upstreams.Count);
        foreach (UpstreamSettings upstream in upstreams)
        {
            normalized.Add(UpstreamAddress.TryNormalize(upstream.BaseAddress, out string? address, out _)
                && address is not null
                    ? upstream with { BaseAddress = address }
                    : upstream);
        }

        return normalized;
    }

    private static string? ReadOptionalString(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TimeSpan ReadTimeSpan(IConfiguration configuration, string key, TimeSpan defaultValue)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan parsed)
            ? parsed
            : throw new InvalidOperationException($"配置项 {key} 不是合法的时间间隔。");
    }

    private static long ReadLong(IConfiguration configuration, string key, long defaultValue)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : throw new InvalidOperationException($"配置项 {key} 不是合法的整数。");
    }

    private static void ThrowIfInvalid(ConfigurationValidationResult validation)
    {
        if (validation.IsValid)
        {
            return;
        }

        string codes = string.Join(", ", validation.Issues.Select(issue => issue.Code));
        throw new InvalidOperationException($"配置校验失败：{codes}。");
    }
}
