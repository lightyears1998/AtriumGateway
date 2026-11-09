using System.Text.Json;

namespace AtriumGateway.Core.Upstream;

/// <summary>上游错误分类结果。</summary>
public enum UpstreamFailureClass
{
    /// <summary>原样透传，不标记、不换发。</summary>
    PassThrough = 0,

    /// <summary>明确错误：可标记不可用并换发。</summary>
    ExplicitFailure = 1,
}

/// <summary>
/// 上游错误观察结果。仅用于判定，不改变响应内容；缺失字段为 null。
/// </summary>
public readonly record struct UpstreamErrorSignals(int StatusCode, string? ErrorType, string? ErrorCode, string? Message);

/// <summary>
/// 依据状态码与错误体字段判定上游错误是否属于「明确错误」。
/// 判定保持只读：错误体仍原样透传给客户端。
/// </summary>
public static class UpstreamErrorClassifier
{
    /// <summary>OpenCode 订阅缺失的 <c>error.type</c>（该服务未遵循 OpenAI 的 error.type 枚举）。</summary>
    private const string PermissionErrorType = "permission_error";

    /// <summary>OpenCode 订阅缺失的错误消息片段。</summary>
    private const string SubscriptionRequiredFragment = "subscription is required";

    public static UpstreamFailureClass Classify(UpstreamErrorSignals signals)
    {
        // 401：该上游凭据不可用，换发有效且不会重复计费。
        if (signals.StatusCode == 401)
        {
            return UpstreamFailureClass.ExplicitFailure;
        }

        if (signals.StatusCode == 403)
        {
            return IsUpstreamUnavailable(signals)
                ? UpstreamFailureClass.ExplicitFailure
                : UpstreamFailureClass.PassThrough;
        }

        if (signals.StatusCode != 429 || string.IsNullOrEmpty(signals.ErrorCode))
        {
            return UpstreamFailureClass.PassThrough;
        }

        return IsQuotaCode(signals.ErrorCode) ? UpstreamFailureClass.ExplicitFailure : UpstreamFailureClass.PassThrough;
    }

    /// <summary>
    /// 从错误体 JSON 只读提取 <c>error.type</c>、<c>error.code</c> 与 <c>error.message</c>。
    /// 只接受 <c>{"error":{...}}</c> 形状；解析失败时各字段为 null。
    /// </summary>
    public static UpstreamErrorSignals ReadSignals(int statusCode, ReadOnlySpan<byte> errorBody)
    {
        UpstreamErrorSignals empty = new(statusCode, null, null, null);
        if (errorBody.IsEmpty)
        {
            return empty;
        }

        try
        {
            Utf8JsonReader reader = new(errorBody, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || !reader.ValueTextEquals("error"u8))
                {
                    continue;
                }

                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return empty;
                }

                (string? errorType, string? errorCode, string? message) = ReadErrorObject(ref reader);
                return new UpstreamErrorSignals(statusCode, errorType, errorCode, message);
            }
        }
        catch (JsonException)
        {
            return empty;
        }

        return empty;
    }

    /// <summary>
    /// HTTP 403 中「该上游整体不可用」的子类：目前只识别 OpenCode订阅缺失这一确定情形。
    /// 其余 403（模型无权、地域限制等）属请求级错误，透传而不标记上游，
    /// 否则一次模型级拒绝会把全部流量迁走。
    /// </summary>
    private static bool IsUpstreamUnavailable(UpstreamErrorSignals signals)
        => string.Equals(signals.ErrorType, PermissionErrorType, StringComparison.OrdinalIgnoreCase)
            && signals.Message is not null
            && signals.Message.Contains(SubscriptionRequiredFragment, StringComparison.OrdinalIgnoreCase);

    private static (string? ErrorType, string? ErrorCode, string? Message) ReadErrorObject(ref Utf8JsonReader reader)
    {
        string? errorType = null;
        string? errorCode = null;
        string? message = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                continue;
            }

            if (reader.ValueTextEquals("type"u8))
            {
                errorType = ReadStringValue(ref reader);
            }
            else if (reader.ValueTextEquals("code"u8))
            {
                errorCode = ReadStringValue(ref reader);
            }
            else if (reader.ValueTextEquals("message"u8))
            {
                message = ReadStringValue(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return (errorType, errorCode, message);
    }

    private static string? ReadStringValue(ref Utf8JsonReader reader)
        => reader.Read() && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }
    }

    private static bool IsQuotaCode(string errorCode)
        => string.Equals(errorCode, "insufficient_quota", StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, "credit_balance_exhausted", StringComparison.OrdinalIgnoreCase)
            || errorCode.EndsWith("_spend_limit_exceeded", StringComparison.OrdinalIgnoreCase)
            || errorCode.EndsWith("_usage_limit_exceeded", StringComparison.OrdinalIgnoreCase);
}
