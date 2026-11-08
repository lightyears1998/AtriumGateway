using System.Net.Http.Headers;
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
    private const int MaxErrorBodyBytes = 64 * 1024;

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
        if (proxyResponse is null || !RequiresInspection(proxyResponse))
        {
            return await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
        }

        Stream errorStream = await proxyResponse.Content.ReadAsStreamAsync(cancellationToken);
        try
        {
            (byte[] prefix, bool complete, string? errorCode) = await ReadPrefixAsync(errorStream, cancellationToken);
            UpstreamFailureClass failureClass = UpstreamErrorClassifier.Classify(
                (int)proxyResponse.StatusCode,
                complete ? errorCode : null);

            await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);

            if (failureClass == UpstreamFailureClass.ExplicitFailure && ForwardingSignals.IsRequestBodyReplayable(httpContext))
            {
                httpContext.Response.Clear();
                ForwardingSignals.SetSwitchNext(httpContext, new UpstreamSwitchReason((int)proxyResponse.StatusCode, errorCode));
                return false;
            }

            await WriteBodyAsync(httpContext, errorStream, prefix, complete, cancellationToken);
            return false;
        }
        finally
        {
            await errorStream.DisposeAsync();
        }
    }

    private static bool RequiresInspection(HttpResponseMessage response)
        => (int)response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status429TooManyRequests;

    private static async ValueTask<(byte[] Prefix, bool Complete, string? ErrorCode)> ReadPrefixAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[MaxErrorBodyBytes];
        int total = 0;

        while (total < buffer.Length)
        {
            int read = await source.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                return (buffer[..total], true, UpstreamErrorClassifier.TryReadErrorCode(buffer.AsSpan(0, total)));
            }

            total += read;
        }

        return (buffer, false, null);
    }

    private static async ValueTask WriteBodyAsync(
        HttpContext context,
        Stream source,
        byte[] prefix,
        bool complete,
        CancellationToken cancellationToken)
    {
        if (prefix.Length > 0)
        {
            await context.Response.Body.WriteAsync(prefix.AsMemory(0, prefix.Length), cancellationToken);
        }

        if (!complete)
        {
            await source.CopyToAsync(context.Response.Body, cancellationToken);
        }
    }

    private static void ApplyFixedUserAgent(HttpRequestMessage request, string userAgent)
    {
        request.Headers.Remove("User-Agent");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    }

    private static void ApplyUpstreamAuthorization(HttpRequestMessage request, string apiKey)
        => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    private static void ClearInboundHostHeader(HttpRequestMessage request)
        => request.Headers.Host = null;
}
