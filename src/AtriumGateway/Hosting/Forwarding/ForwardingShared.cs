using Yarp.ReverseProxy.Forwarder;

namespace AtriumGateway.Hosting.Forwarding;

/// <summary>
/// 进程级共享的转发资源：出站连接池、请求配置与固定 User-Agent；目标地址与转换器按上游单独实例化。
/// 持有连接池，须由依赖注入容器在应用关闭时释放。
/// </summary>
public sealed record ForwardingShared(
    HttpMessageInvoker HttpClient,
    ForwarderRequestConfig RequestConfig,
    string UserAgent) : IDisposable
{
    /// <summary>释放出站连接池。</summary>
    public void Dispose() => HttpClient.Dispose();
}
