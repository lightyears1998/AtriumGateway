using AtriumGateway.Core.Upstream;
using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Forwarding;

/// <summary>
/// 处理器与转换器之间的换发信号，经 <see cref="HttpContext.Items"/> 传递。
/// 仅在上游响应字节尚未写出前有效；每次尝试只应消费一次。
/// </summary>
public static class ForwardingSignals
{
    private const string SwitchNextKey = "AtriumGateway:SwitchNextUpstream";
    private const string RequestBodyReplayableKey = "AtriumGateway:RequestBodyReplayable";

    /// <summary>标记本次尝试需要换发，并携带触发原因（上游状态码与错误码）。</summary>
    public static void SetSwitchNext(HttpContext context, UpstreamSwitchReason reason)
        => context.Items[SwitchNextKey] = reason;

    /// <summary>读取并清除换发信号；未设置时返回 false，<paramref name="reason"/> 为 null。</summary>
    public static bool TryConsumeSwitchNext(HttpContext context, out UpstreamSwitchReason? reason)
    {
        if (context.Items.TryGetValue(SwitchNextKey, out object? value) && value is UpstreamSwitchReason switchReason)
        {
            context.Items.Remove(SwitchNextKey);
            reason = switchReason;
            return true;
        }

        reason = null;
        return false;
    }

    /// <summary>记录本次请求的请求体是否可重放，供响应侧判定能否换发。</summary>
    public static void SetRequestBodyReplayable(HttpContext context, bool replayable)
        => context.Items[RequestBodyReplayableKey] = replayable;

    /// <summary>请求体是否可重放（空体或已在上限内缓冲）；未设置时视为不可重放。</summary>
    public static bool IsRequestBodyReplayable(HttpContext context)
        => context.Items.TryGetValue(RequestBodyReplayableKey, out object? value) && value is true;
}
