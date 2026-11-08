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
            Upstreams = ReadUpstreamList(configuration, UpstreamsKey),
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
        => throw new NotImplementedException(); // TODO：读取 Gateway:Accounts:{i}:{Id,Key}，保持配置顺序

    private static IReadOnlyList<UpstreamSettings> ReadUpstreamList(IConfiguration configuration, string key)
        => throw new NotImplementedException(); // TODO：读取 Gateway:Upstreams:{i}:{Id,BaseAddress,ApiKey}，顺序即固定优先级

    private static string? ReadOptionalString(IConfiguration configuration, string key)
        => throw new NotImplementedException(); // TODO

    private static TimeSpan ReadTimeSpan(IConfiguration configuration, string key, TimeSpan defaultValue)
        => throw new NotImplementedException(); // TODO：缺失回退默认值，非法则抛配置异常

    private static long ReadLong(IConfiguration configuration, string key, long defaultValue)
        => throw new NotImplementedException(); // TODO

    private static void ThrowIfInvalid(ConfigurationValidationResult validation)
        => throw new NotImplementedException(); // TODO：汇总问题快速失败；只说明缺哪个键，不回显取值
}
