using AtriumGateway.Core.Upstream;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace AtriumGateway.Hosting.Forwarding;

/// <summary>
/// 受控改写与换发信号。
/// 请求侧仅替换 <c>Authorization</c> 与 <c>User-Agent</c>，并让 <c>Host</c> 由目标地址派生；
/// 响应侧仅对 401/429 做有界只读判定，不改写任何响应内容。
/// 基类调用只复制协议头，不附加 <c>X-Forwarded-*</c>。
/// </summary>
internal sealed class GatewayTransformer : HttpTransformer
{
    private readonly string _upstreamApiKey;
    private readonly string _userAgent;

    public GatewayTransformer(string upstreamApiKey, string userAgent)
    {
        _upstreamApiKey = upstreamApiKey;
        _userAgent = userAgent;
    }

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
        ApplyFixedUserAgent(proxyRequest, _userAgent);
        ApplyUpstreamAuthorization(proxyRequest, _upstreamApiKey);
        ClearInboundHostHeader(proxyRequest);
    }

    /// <summary>
    /// 响应到达时调用，早于任何响应字节写出。需要判定时返回 false 表示不转发正文，由本类自行处置。
    /// </summary>
    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken)
    {
        if (proxyResponse is not null && RequiresInspection(proxyResponse))
        {
            return await InspectAndHandleAsync(httpContext, proxyResponse, cancellationToken);
        }

        return await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
    }

    private static bool RequiresInspection(HttpResponseMessage response)
        => throw new NotImplementedException(); // TODO：状态码为 401 或 429（统一走读体路径以保留原样透传能力）

    // 只读提取 error.code → 分类 → 基类复制状态码与响应头 → 明确错误且请求体可重放则清空响应并置入换发信号
    //（此时尚无字节写出）→ 否则把已读字节原样写回。
    private async ValueTask<bool> InspectAndHandleAsync(
        HttpContext context,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        (ReadOnlyMemory<byte> errorBody, string? errorCode) = await ReadErrorBodyAsync(response, cancellationToken);
        UpstreamFailureClass failureClass = UpstreamErrorClassifier.Classify((int)response.StatusCode, errorCode);

        await base.TransformResponseAsync(context, response, cancellationToken);

        if (failureClass == UpstreamFailureClass.ExplicitFailure && ForwardingSignals.IsRequestBodyReplayable(context))
        {
            context.Response.Clear();
            ForwardingSignals.SetSwitchNext(context, new UpstreamSwitchReason((int)response.StatusCode, errorCode));
            return false;
        }

        await WriteBackErrorBodyAsync(context, errorBody, cancellationToken);
        return false;
    }

    private static ValueTask<(ReadOnlyMemory<byte> Body, string? ErrorCode)> ReadErrorBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：有界读取错误体，并用 UpstreamErrorClassifier.TryReadErrorCode 提取 code

    private static ValueTask WriteBackErrorBodyAsync(
        HttpContext context,
        ReadOnlyMemory<byte> errorBody,
        CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：把已读字节原样写入响应体（状态码与响应头已由基类复制）

    private static void ApplyFixedUserAgent(HttpRequestMessage request, string userAgent)
        => throw new NotImplementedException(); // TODO：清除后 TryAddWithoutValidation

    private static void ApplyUpstreamAuthorization(HttpRequestMessage request, string apiKey)
        => throw new NotImplementedException(); // TODO：丢弃客户端凭据，写入所选上游密钥

    private static void ClearInboundHostHeader(HttpRequestMessage request)
        => throw new NotImplementedException(); // TODO：置 null，由目标 URI 决定 Host
}
