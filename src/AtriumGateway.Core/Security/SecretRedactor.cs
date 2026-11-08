namespace AtriumGateway.Core.Security;

/// <summary>日志与异常文本的脱敏。</summary>
public static class SecretRedactor
{
    public const string Placeholder = "<redacted>";

    /// <summary>把文本中出现的任一密钥替换为占位符；空文本返回 null。</summary>
    public static string? Redact(string? value, IEnumerable<string> secrets)
        => throw new NotImplementedException(); // TODO
}
