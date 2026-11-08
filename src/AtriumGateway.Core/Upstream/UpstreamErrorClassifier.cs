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

/// <summary>依据状态码与 <c>error.code</c> 判定上游错误是否属于「明确错误」。</summary>
public static class UpstreamErrorClassifier
{
    public static UpstreamFailureClass Classify(int statusCode, string? errorCode)
    {
        if (statusCode == 401)
        {
            return UpstreamFailureClass.ExplicitFailure;
        }

        if (statusCode != 429 || string.IsNullOrEmpty(errorCode))
        {
            return UpstreamFailureClass.PassThrough;
        }

        return IsQuotaCode(errorCode) ? UpstreamFailureClass.ExplicitFailure : UpstreamFailureClass.PassThrough;
    }

    /// <summary>从错误体 JSON 只读提取 <c>error.code</c>；不可得为 null。</summary>
    public static string? TryReadErrorCode(ReadOnlySpan<byte> errorBody)
    {
        if (errorBody.IsEmpty)
        {
            return null;
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
                    return null;
                }

                return ReadCodeFromErrorObject(ref reader);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ReadCodeFromErrorObject(ref Utf8JsonReader reader)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("code"u8))
            {
                return reader.Read() && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }
        }

        return null;
    }

    private static bool IsQuotaCode(string errorCode)
        => string.Equals(errorCode, "insufficient_quota", StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, "credit_balance_exhausted", StringComparison.OrdinalIgnoreCase)
            || errorCode.EndsWith("spend_limit_exceeded", StringComparison.OrdinalIgnoreCase)
            || errorCode.EndsWith("usage_limit_exceeded", StringComparison.OrdinalIgnoreCase);
}
