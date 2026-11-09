# 部署指南（Linux · systemd · 源码编译 · NativeAOT 部署）

## 1. 部署拓扑

```
客户端（OpenAI SDK / curl）
        │  HTTPS（TLS 由前置代理终止）
        ▼
   nginx / Caddy  ── HTTP ──►  Atrium Gateway（127.0.0.1:1109，systemd 服务）
                                   │  HTTPS
                                   ▼
                          上游 OpenAI 兼容端点
```

- 网关自身**只监听 HTTP**，不负责证书与 TLS 终止；
- 使用 NativeAOT 部署。

## 2. 编译与发布

仓库中宿主工程已设置 `<PublishAot>true</PublishAot>`，发布时编译为本平台 NativeAOT 程序：

```bash
cd /path/to/AtriumGateway
REL=/path/to/AtriumGateway/bin

dotnet restore AtriumGateway.slnx
dotnet publish src/AtriumGateway/AtriumGateway.csproj \
    -c Release \
    --self-contained true \
    -p:PublishAot=true \
    -p:StripSymbols=true \
    -o "$REL"
```

产物为单个原生可执行文件：`$REL/AtriumGateway`

可选参数：

- `-p:InvariantGlobalization=true`：去掉对 ICU 的依赖（会改变全球化/culture 行为，仅在确认无需求时使用）；
- `-p:IlcGenerateStackTraceData=true`：保留更完整的 AOT 异常栈（体积略增，排障期可用）。

## 3. 目录与运行用户

```text
/opt/atrium-gateway/
└── AtriumGateway                             # 原生可执行文件（由仓库 bin/ 安装）

/etc/atrium-gateway/
└── appsettings.json                          # 配置（含密钥，644 开放读）
```

安装（不新建账号，直接以系统自带的 `nobody` 运行）：

```bash
sudo install -d -m 0755 /opt/atrium-gateway /etc/atrium-gateway
sudo install -m 0755 /path/to/AtriumGateway/bin/AtriumGateway /opt/atrium-gateway/AtriumGateway
sudo install -m 0644 src/AtriumGateway/appsettings.json.example /etc/atrium-gateway/appsettings.json
```

- 运行用户为 `nobody`，属组使用发行版对应的无特权组（Debian/Ubuntu 为 `nogroup`，部分发行版为 `nobody`，见下文 systemd 单元）；
- `nobody` 为多服务共用的系统账号，权限最小但并非独占隔离；若后续需要独立审计或更严格隔离，再考虑专用账号；
- `nobody` 无需家目录；应用不写磁盘，不需要额外可写目录。

## 4. 配置

### 4.1 配置来源与优先级

工作目录即内容根（unit 中 `WorkingDirectory=/etc/atrium-gateway`），因此应用会读取该目录下的：

1. `appsettings.json`
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json`（可选）

**后出现的覆盖先出现的**。密钥放在 `appsettings.json` 中，不入库。

### 4.2 配置项

| 键 | 必填 | 说明 |
|---|---|---|
| `Gateway:Accounts[]` | 是 | 入站账号：`Id`（日志标识）、`Key`（调用方使用的密钥） |
| `Gateway:Upstreams[]` | 是 | 上游：`Id`、`BaseAddress`、`ApiKey`；**数组顺序即初始优先级** |
| `Gateway:UserAgent` | 否 | 发往上游的固定 `User-Agent`；缺省 `Atrium-Gateway/<version>` |
| `Gateway:UpstreamActivityTimeout` | 否 | 上游空闲超时，默认 `00:10:00`；代理侧读超时须大于它 |
| `Gateway:UpstreamCooldown` | 否 | 明确错误后的冷却期，默认 `00:05:00` |
| `Gateway:RequestBodyBufferLimit` | 否 | 请求体可重放缓冲上限，默认 `4194304` 字节；超限则不换发 |

## 5. systemd 单元

`/etc/systemd/system/atrium-gateway.service`：

```ini
[Unit]
Description=Atrium Gateway
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=nobody
Group=nogroup
WorkingDirectory=/etc/atrium-gateway
ExecStart=/opt/atrium-gateway/AtriumGateway

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_NOLOGO=1

# Life cycle
Restart=always
RestartSec=2
TimeoutStopSec=30
KillSignal=SIGTERM
UMask=0077
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
```

## 6. 日志与运维

```bash
journalctl -u atrium-gateway -f                    # 跟踪
journalctl -u atrium-gateway --since '10 min ago'  # 排查
journalctl -u atrium-gateway -o cat | tail -n 50   # 无前缀时间戳
```

## 7. 前置代理（TLS 终止）

### 7.1 nginx

```nginx
server {
    listen 443 ssl http2;
    server_name gateway.example.com;

    underscores_in_headers on;

    ssl_certificate     /etc/nginx/tls/gateway.example.com.crt;
    ssl_certificate_key /etc/nginx/tls/gateway.example.com.key;

    location / {
        proxy_pass http://127.0.0.1:1109;
        proxy_http_version 1.1;

        proxy_buffering off;
        proxy_request_buffering off;
        proxy_read_timeout 660s;
        proxy_send_timeout 660s;

        client_max_body_size 100m;
    }
}
```

- 应启用 `underscores_in_headers`，因为部分客户端可能发送带有下划线的 HTTP 头。例如 DSH 可能发送 `session_id` 头；
- 为支持 SSE，`proxy_buffering` 和 `proxy_request_buffering` 应关闭缓冲；
- `proxy_read_timeout` 和 `proxy_send_timeout` 必须大于 Gateway:UpstreamActivityTimeout（默认 600s）。

## 8. 附：参考资料

- 构建与包版本：以 `Directory.Build.props`、`Directory.Packages.props`、`*.csproj` 为单一事实源；
- 配置形状：`src/AtriumGateway/appsettings.json.example`；
- NativeAOT 前置条件：<https://learn.microsoft.com/dotnet/core/deploying/native-aot/>；
- ASP.NET Core + NativeAOT：<https://learn.microsoft.com/aspnet/core/fundamentals/native-aot>。
