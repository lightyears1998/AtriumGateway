using System.Diagnostics.CodeAnalysis;

namespace AtriumGateway.Core.Authentication;

/// <summary>鉴权结果：仅暴露账号标识；密钥只在校验内部使用，永不输出。</summary>
public sealed record AuthenticatedAccount(string Id);

/// <summary>入站账号校验：OpenAI 风格 Bearer 令牌，恒定时间比较。</summary>
public interface IAccountAuthenticator
{
    /// <summary>校验入站令牌；空令牌或无匹配账号时返回 false，<paramref name="account"/> 为 null。</summary>
    bool TryAuthenticate(string? bearerToken, [NotNullWhen(true)] out AuthenticatedAccount? account);
}
