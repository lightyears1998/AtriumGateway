namespace AtriumGateway.Core.Identity;

/// <summary>发往上游的 User-Agent 组装。</summary>
public static class UserAgentComposer
{
    public const string DefaultProductName = "Atrium-Gateway";

    /// <summary>配置值优先；否则生成 <c>Atrium-Gateway/{version}</c>。</summary>
    public static string Compose(string? configured, Version gatewayVersion)
        => throw new NotImplementedException(); // TODO

    /// <summary>
    /// 读取网关自身程序集版本。
    /// 未设置版本号时回退 0.0.0。
    /// </summary>
    public static Version ReadGatewayVersion()
        => typeof(UserAgentComposer).Assembly.GetName().Version ?? new Version(0, 0, 0);
}
