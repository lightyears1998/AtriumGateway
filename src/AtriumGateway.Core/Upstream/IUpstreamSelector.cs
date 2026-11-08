namespace AtriumGateway.Core.Upstream;

/// <summary>一次转发结果的分类（用于更新内存可用性状态）。</summary>
public enum UpstreamOutcome
{
    /// <summary>正常响应、客户端错误与网关错误：活动上游固定在该处，状态不变。</summary>
    PassThrough = 0,

    /// <summary>明确错误（配额类 429 / 上游 401）：标记不可用并进入冷却，活动上游前移一个。</summary>
    ExplicitFailure = 1,
}

public interface IUpstreamSelector
{
    /// <summary>租用当前可用上游；全部上游都在冷却中时返回 null。租约必须在使用后释放。</summary>
    UpstreamLease? Acquire();
}
