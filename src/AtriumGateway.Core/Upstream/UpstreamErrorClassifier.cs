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
        => throw new NotImplementedException(); // TODO：401 → ExplicitFailure；429 且 code 属配额/计费类 → ExplicitFailure；其余（含客户端错误与 5xx）PassThrough

    /// <summary>从错误体 JSON 只读提取 <c>error.code</c>（有界、<c>Utf8JsonReader</c>、AOT 安全）；不可得为 null。</summary>
    public static string? TryReadErrorCode(ReadOnlySpan<byte> errorBody)
        => throw new NotImplementedException(); // TODO
}
