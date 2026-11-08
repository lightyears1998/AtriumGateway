using AtriumGateway.Core.Configuration;

namespace AtriumGateway.Core.Upstream;

/// <summary>上游选择器：配置顺序固定优先级 + 内存冷却状态（草稿阶段未实现）。</summary>
public sealed class UpstreamSelector : IUpstreamSelector
{
    private readonly GatewayOptions _options;

    public UpstreamSelector(GatewayOptions options) => _options = options;

    /// <summary>供启动自检读取的上游数量（避免字段未读取）。</summary>
    internal int UpstreamCount => _options.Upstreams.Count;

    public UpstreamEndpoint? SelectNext(IReadOnlyList<string> attemptedUpstreamIds)
        => throw new NotImplementedException(); // TODO：按 _options.Upstreams 顺序，跳过已试过与仍在冷却中的，返回第一个

    public void Report(UpstreamEndpoint upstream, UpstreamOutcome outcome, DateTimeOffset now)
        => throw new NotImplementedException(); // TODO：仅 ExplicitFailure → 记入内存冷却截止（now + _options.UpstreamCooldown）；不持久化
}
