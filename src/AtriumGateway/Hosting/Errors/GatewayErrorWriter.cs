using AtriumGateway.Core;
using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Errors;

/// <summary>
/// 网关自身错误输出：OpenAI 风格 error JSON。
/// 401 鉴权失败、503 全部上游不可用；字段细节待对照 OpenAI 规格核实。
/// 序列化须 AOT 安全（源生成或手写字节），不得使用反射式 <c>JsonSerializer</c>。
/// </summary>
public sealed class GatewayErrorWriter
{
    public Task WriteAsync(HttpContext context, GatewayErrorKind kind, CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：kind → 状态码 + application/json + {"error":{...}}；消息不含密钥与上游细节
}
