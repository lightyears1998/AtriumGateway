using System.Diagnostics;

namespace AtriumGateway.Core.Upstream;

/// <summary>
/// 一次上游尝试的租约：由 <see cref="IUpstreamSelector.Acquire"/> 取得，代表「已选中该上游」。
/// 使用方必须在结束时上报结果（每个租约只能上报一次）；若未上报即释放，按
/// <see cref="UpstreamOutcome.ExplicitFailure"/> 兜底并触发调试断言。
/// </summary>
public sealed class UpstreamLease : IDisposable
{
    private const int StateOpen = 0;
    private const int StateReported = 1;

    private readonly IUpstreamHealth _health;
    private int _state;

    internal UpstreamLease(IUpstreamHealth health, UpstreamEndpoint upstream)
    {
        _health = health;
        Upstream = upstream;
    }

    /// <summary>本次租用选中的上游。</summary>
    public UpstreamEndpoint Upstream { get; }

    /// <summary>上报本次尝试的结果；仅允许调用一次，再次调用抛 <see cref="InvalidOperationException"/>。</summary>
    public void Report(UpstreamOutcome outcome)
    {
        if (Interlocked.Exchange(ref _state, StateReported) == StateReported)
        {
            throw new InvalidOperationException("同一个上游租约只能上报一次结果。");
        }

        _health.Report(Upstream, outcome, DateTimeOffset.UtcNow);
    }

    /// <summary>释放租约；若未上报结果，则以 <see cref="UpstreamOutcome.ExplicitFailure"/> 兜底。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, StateReported) == StateReported)
        {
            return;
        }

        Debug.Fail("上游租约在使用结束前未上报结果，已按 ExplicitFailure 兜底。");
        _health.Report(Upstream, UpstreamOutcome.ExplicitFailure, DateTimeOffset.UtcNow);
    }
}

internal interface IUpstreamHealth
{
    void Report(UpstreamEndpoint upstream, UpstreamOutcome outcome, DateTimeOffset now);
}
