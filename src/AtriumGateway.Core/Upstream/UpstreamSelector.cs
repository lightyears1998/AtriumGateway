using AtriumGateway.Core.Configuration;

namespace AtriumGateway.Core.Upstream;

/// <summary>
/// 固定顺序的上游选择与内存可用性状态。
/// 状态只在真实响应驱动下变化：明确错误进入冷却，冷却到期后由后续请求自然重试；进程重启即清零。
/// </summary>
public sealed class UpstreamSelector : IUpstreamSelector
{
    private readonly UpstreamEndpoint[] _upstreams;
    private readonly TimeSpan _cooldown;
    private readonly Dictionary<string, DateTimeOffset> _coolingUntil = new(StringComparer.Ordinal);
    private readonly object _gate = new();

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

    public UpstreamEndpoint? SelectNext(IReadOnlyList<string> attemptedUpstreamIds)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            foreach (UpstreamEndpoint upstream in _upstreams)
            {
                if (Contains(attemptedUpstreamIds, upstream.Id))
                {
                    continue;
                }

                if (_coolingUntil.TryGetValue(upstream.Id, out DateTimeOffset until) && until > now)
                {
                    continue;
                }

                return upstream;
            }

            return null;
        }
    }

    public void Report(UpstreamEndpoint upstream, UpstreamOutcome outcome, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (outcome == UpstreamOutcome.ExplicitFailure)
            {
                _coolingUntil[upstream.Id] = now + _cooldown;
            }
            else
            {
                _coolingUntil.Remove(upstream.Id);
            }
        }
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string baseAddress)
        => UpstreamAddress.TryNormalize(baseAddress, out string? normalized, out _) && normalized is not null
            ? normalized
            : baseAddress;
}
