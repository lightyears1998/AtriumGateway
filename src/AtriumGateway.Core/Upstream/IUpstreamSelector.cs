namespace AtriumGateway.Core.Upstream;

/// <summary>一次转发结果的分类（用于更新内存可用性状态）。</summary>
public enum UpstreamOutcome
{
    /// <summary>正常响应、客户端错误与网关错误：状态不变。</summary>
    PassThrough = 0,

    /// <summary>明确错误（配额类 429 / 上游 401）：标记不可用并进入冷却。</summary>
    ExplicitFailure = 1,
}

/// <summary>固定顺序的上游选择与内存可用性状态：无探测、无持久化、重启清零。</summary>
public interface IUpstreamSelector
{
    /// <summary>按配置固定顺序返回第一个「可用（非冷却中）且未被本次请求试过」的上游；无则为 null。</summary>
    UpstreamEndpoint? SelectNext(IReadOnlyList<string> attemptedUpstreamIds);

    /// <summary>用一次真实结果更新内存状态；仅 <see cref="UpstreamOutcome.ExplicitFailure"/> 改变状态。</summary>
    void Report(UpstreamEndpoint upstream, UpstreamOutcome outcome, DateTimeOffset now);
}
