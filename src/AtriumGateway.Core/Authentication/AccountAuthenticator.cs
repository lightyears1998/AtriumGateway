using System.Diagnostics.CodeAnalysis;
using AtriumGateway.Core.Configuration;

namespace AtriumGateway.Core.Authentication;

/// <summary>以配置的账号密钥集合校验入站令牌（草稿阶段未实现）。</summary>
public sealed class AccountAuthenticator : IAccountAuthenticator
{
    private readonly GatewayOptions _options;

    public AccountAuthenticator(GatewayOptions options) => _options = options;

    /// <summary>供启动自检读取的账号数量（避免字段未读取）。</summary>
    internal int AccountCount => _options.Accounts.Count;

    public bool TryAuthenticate(string? bearerToken, [NotNullWhen(true)] out AuthenticatedAccount? account)
        => throw new NotImplementedException(); // TODO：跳过 "Bearer " 前缀后与各账号 Key 做恒定时间比较；命中返回账号 Id
}
