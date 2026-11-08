namespace AtriumGateway.Core.Configuration;

/// <summary>入站账号设置（OpenAI 风格 Bearer 密钥）；Key 永不入库、永不入日志。</summary>
public sealed record AccountSettings(string Id, string Key);

/// <summary>上游设置；列表顺序即固定优先级；ApiKey 永不入库、永不入日志。</summary>
public sealed record UpstreamSettings(string Id, string BaseAddress, string ApiKey);

/// <summary>网关运行参数。仅由宿主在启动时从配置读取并校验，运行期不再变化。</summary>
public sealed class GatewayOptions
{
    /// <summary>入站账号集合；启动时校验非空、Id 与 Key 唯一。</summary>
    public IReadOnlyList<AccountSettings> Accounts { get; init; } = Array.Empty<AccountSettings>();

    /// <summary>上游列表；顺序即固定选择优先级（不做轮转/权重）。</summary>
    public IReadOnlyList<UpstreamSettings> Upstreams { get; init; } = Array.Empty<UpstreamSettings>();

    /// <summary>发往上游的 User-Agent 固定值；为空时由 <c>UserAgentComposer</c> 生成默认值。</summary>
    public string? UserAgent { get; set; }

    /// <summary>上游空闲超时（无任何读写活动即计时），默认 10 分钟。</summary>
    public TimeSpan UpstreamActivityTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>上游标记不可用后的冷却期，默认 5 分钟；到期由下一次真实请求自然试探恢复。</summary>
    public TimeSpan UpstreamCooldown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>换发所需的请求体缓冲上限，默认 4MB；超过则该请求不可换发（透传上游错误）。</summary>
    public long RequestBodyBufferLimit { get; set; } = 4L * 1024 * 1024;
}
