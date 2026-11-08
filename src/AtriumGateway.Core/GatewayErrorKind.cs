namespace AtriumGateway.Core;

/// <summary>网关自身生成的错误（OpenAI 风格 error JSON）。</summary>
public enum GatewayErrorKind
{
    /// <summary>入站鉴权失败（HTTP 401），请求不触达上游。</summary>
    Unauthorized = 401,

    /// <summary>全部上游不可用（HTTP 503）。</summary>
    NoUpstreamAvailable = 503,
}
