using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using AtriumGateway.Core.Configuration;

namespace AtriumGateway.Core.Authentication;

/// <summary>以配置的账号密钥集合校验入站令牌；比较为恒定时间，且遍历全部账号不提前返回。</summary>
public sealed class AccountAuthenticator : IAccountAuthenticator
{
    private readonly AccountSettings[] _accounts;

    public AccountAuthenticator(GatewayOptions options)
    {
        _accounts = options.Accounts.ToArray();
    }

    public bool TryAuthenticate(string? bearerToken, [NotNullWhen(true)] out AuthenticatedAccount? account)
    {
        account = null;
        if (string.IsNullOrEmpty(bearerToken))
        {
            return false;
        }

        byte[] tokenBytes = Encoding.UTF8.GetBytes(bearerToken);
        AuthenticatedAccount? matched = null;

        foreach (AccountSettings candidate in _accounts)
        {
            byte[] candidateBytes = Encoding.UTF8.GetBytes(candidate.Key);
            if (CryptographicOperations.FixedTimeEquals(tokenBytes, candidateBytes))
            {
                matched ??= new AuthenticatedAccount(candidate.Id);
            }
        }

        account = matched;
        return matched is not null;
    }
}
