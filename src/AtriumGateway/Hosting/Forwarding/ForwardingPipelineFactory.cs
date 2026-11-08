using System.Net;
using AtriumGateway.Core.Configuration;
using AtriumGateway.Core.Identity;
using AtriumGateway.Core.Upstream;
using Yarp.ReverseProxy.Forwarder;

namespace AtriumGateway.Hosting.Forwarding;

/// <summary>构建进程级 <see cref="ForwardingShared"/> 与每上游 <see cref="HttpTransformer"/>。</summary>
public sealed class ForwardingPipelineFactory
{
    // 出站连接参数。
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);
    private const int MaxConnectionsPerServer = 256;

    private readonly GatewayOptions _options;

    public ForwardingPipelineFactory(GatewayOptions options) => _options = options;

    /// <summary>创建出站客户端、请求配置与固定 User-Agent，进程内共享一次。</summary>
    public ForwardingShared CreateShared()
    {
        HttpMessageInvoker httpClient = CreateHttpClient();
        ForwarderRequestConfig requestConfig = CreateRequestConfig(_options);
        string userAgent = UserAgentComposer.Compose(_options.UserAgent, UserAgentComposer.ReadGatewayVersion());
        return new ForwardingShared(httpClient, requestConfig, userAgent);
    }

    /// <summary>为指定上游创建转换器：注入该上游密钥与共享 User-Agent。</summary>
    public HttpTransformer CreateTransformer(UpstreamEndpoint upstream, ForwardingShared shared)
        => new GatewayTransformer(upstream.ApiKey, shared.UserAgent);

    // 出站连接池：不继承环境代理，不做自动重定向、解压与 Cookie，关闭追踪头注入；连接与连接池参数取本类常量。
    private static HttpMessageInvoker CreateHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = null,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = ConnectTimeout,
            PooledConnectionLifetime = PooledConnectionLifetime,
            PooledConnectionIdleTimeout = PooledConnectionIdleTimeout,
            MaxConnectionsPerServer = MaxConnectionsPerServer,
        };

        return new HttpMessageInvoker(handler);
    }

    // 请求级配置：空闲超时取配置值；响应缓冲保持关闭以支持流式；出站 HTTP 版本策略待评审，暂用框架默认。
    private static ForwarderRequestConfig CreateRequestConfig(GatewayOptions options)
        => new()
        {
            ActivityTimeout = options.UpstreamActivityTimeout,
        };
}
