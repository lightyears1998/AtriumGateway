using AtriumGateway.Core.Configuration;

namespace AtriumGateway.Core.Upstream;

/// <summary>
/// 粘性上游选择与内存可用性状态。
/// 只维护进程级状态（活动游标与冷却标记），不记录单次请求的尝试历史：
/// 明确错误经租约上报后立即置冷却，冷却期内不会再被选中；活动游标只在明确错误时前移、成功后钉住，
/// 冷却到期不会把它回退，仅当环形上所有上游都不可用时 <see cref="Acquire"/> 才返回 null。
/// 进程重启即清零。
/// </summary>
public sealed class UpstreamSelector : IUpstreamSelector, IUpstreamHealth
{
    private readonly UpstreamEndpoint[] _upstreams;
    private readonly TimeSpan _cooldown;
    private readonly Dictionary<string, DateTimeOffset> _coolingUntil = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private int _activeIndex;

    public UpstreamSelector(GatewayOptions options)
    {
        _upstreams = options.Upstreams
            .Select(upstream => new UpstreamEndpoint(
                upstream.Id,
                Normalize(upstream.BaseAddress),
                upstream.ApiKey))
            .ToArray();
        _cooldown = options.UpstreamCooldown;
    }

    public UpstreamLease? Acquire()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            for (int offset = 0; offset < _upstreams.Length; offset++)
            {
                int index = (_activeIndex + offset) % _upstreams.Length;
                UpstreamEndpoint upstream = _upstreams[index];

                if (_coolingUntil.TryGetValue(upstream.Id, out DateTimeOffset until) && until > now)
                {
                    continue;
                }

                return new UpstreamLease(this, upstream);
            }

            return null;
        }
    }

    void IUpstreamHealth.Report(UpstreamEndpoint upstream, UpstreamOutcome outcome, DateTimeOffset now)
    {
        int index = IndexOf(upstream.Id);
        if (index < 0)
        {
            return;
        }

        lock (_gate)
        {
            if (outcome == UpstreamOutcome.ExplicitFailure)
            {
                _coolingUntil[upstream.Id] = now + _cooldown;
                _activeIndex = (index + 1) % _upstreams.Length;
            }
            else
            {
                _coolingUntil.Remove(upstream.Id);
                _activeIndex = index;
            }
        }
    }

    private int IndexOf(string upstreamId)
    {
        for (int index = 0; index < _upstreams.Length; index++)
        {
            if (string.Equals(_upstreams[index].Id, upstreamId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string Normalize(string baseAddress)
        => UpstreamAddress.TryNormalize(baseAddress, out string? normalized, out _) && normalized is not null
            ? normalized
            : baseAddress;
}
