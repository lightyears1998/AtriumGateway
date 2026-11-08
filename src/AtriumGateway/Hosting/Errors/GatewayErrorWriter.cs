using System.Buffers;
using System.Text.Json;
using AtriumGateway.Core;
using Microsoft.AspNetCore.Http;

namespace AtriumGateway.Hosting.Errors;

/// <summary>
/// 网关自身错误输出：OpenAI 风格 error JSON。
/// 401 鉴权失败、503 全部上游不可用；消息不含密钥与上游细节。
/// </summary>
public sealed class GatewayErrorWriter
{
    public async Task WriteAsync(HttpContext context, GatewayErrorKind kind, CancellationToken cancellationToken)
    {
        (int statusCode, string type, string code, string message) = Describe(kind);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("error");
            writer.WriteString("message", message);
            writer.WriteString("type", type);
            writer.WriteNull("param");
            writer.WriteString("code", code);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        await context.Response.Body.WriteAsync(buffer.WrittenMemory, cancellationToken);
    }

    private static (int StatusCode, string Type, string Code, string Message) Describe(GatewayErrorKind kind)
        => kind switch
        {
            GatewayErrorKind.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "invalid_request_error",
                "invalid_api_key",
                "Missing or invalid API key."),
            GatewayErrorKind.NoUpstreamAvailable => (
                StatusCodes.Status503ServiceUnavailable,
                "server_error",
                "upstream_unavailable",
                "No upstream is currently available."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
