using AtriumGateway.Core;
using AtriumGateway.Core.Authentication;
using AtriumGateway.Core.Configuration;
using AtriumGateway.Core.Logging;
using AtriumGateway.Core.Observation;
using AtriumGateway.Core.Security;
using AtriumGateway.Core.Upstream;
using AtriumGateway.Hosting.Errors;
using AtriumGateway.Hosting.Http;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace AtriumGateway.Hosting.Forwarding;

/// <summary>一次上游尝试的结果。</summary>
public enum ForwardAttemptResult
{
    /// <summary>已终结：响应已透传并记录，或网关错误已写出。</summary>
    Completed = 0,

    /// <summary>命中明确错误且可换发：继续下一个上游。</summary>
    SwitchNext = 1,
}

/// <summary>终端处理器：鉴权 → 请求体可重放缓冲 → 固定顺序换发循环 → 只读观测与元数据日志。</summary>
public sealed class GatewayRequestHandler
{
    private readonly IHttpForwarder _forwarder;
    private readonly IAccountAuthenticator _authenticator;
    private readonly IUpstreamSelector _upstreamSelector;
    private readonly ForwardingPipelineFactory _pipelineFactory;
    private readonly ForwardingShared _shared;
    private readonly RequestBodyBufferFactory _bodyBufferFactory;
    private readonly IResponseObserverFactory _observerFactory;
    private readonly GatewayErrorWriter _errorWriter;
    private readonly IRequestLogSink _logSink;
    private readonly string[] _secrets;

    public GatewayRequestHandler(
        IHttpForwarder forwarder,
        IAccountAuthenticator authenticator,
        IUpstreamSelector upstreamSelector,
        ForwardingPipelineFactory pipelineFactory,
        ForwardingShared shared,
        RequestBodyBufferFactory bodyBufferFactory,
        IResponseObserverFactory observerFactory,
        GatewayErrorWriter errorWriter,
        IRequestLogSink logSink,
        GatewayOptions options)
    {
        _forwarder = forwarder;
        _authenticator = authenticator;
        _upstreamSelector = upstreamSelector;
        _pipelineFactory = pipelineFactory;
        _shared = shared;
        _bodyBufferFactory = bodyBufferFactory;
        _observerFactory = observerFactory;
        _errorWriter = errorWriter;
        _logSink = logSink;
        _secrets = CollectSecrets(options);
    }

    public async Task HandleAsync(HttpContext context)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        string? bearerToken = ExtractBearerToken(context);
        if (!_authenticator.TryAuthenticate(bearerToken, out AuthenticatedAccount? account))
        {
            await _errorWriter.WriteAsync(context, GatewayErrorKind.Unauthorized, context.RequestAborted);
            return;
        }

        using RequestBodyBuffer bodyBuffer = _bodyBufferFactory.Create(context);
        ForwardingSignals.SetRequestBodyReplayable(context, bodyBuffer.IsReplayable);

        List<string> attemptedUpstreams = [];
        List<UpstreamSwitchReason> switchReasons = [];

        while (true)
        {
            UpstreamEndpoint? upstream = _upstreamSelector.SelectNext(attemptedUpstreams);
            if (upstream is null)
            {
                break;
            }

            attemptedUpstreams.Add(upstream.Id);
            bodyBuffer.Rewind();

            ForwardAttemptResult attempt = await ForwardOnceAsync(
                context, upstream, account, startedAt, attemptedUpstreams, switchReasons);

            if (attempt == ForwardAttemptResult.Completed)
            {
                return;
            }
        }

        await _errorWriter.WriteAsync(context, GatewayErrorKind.NoUpstreamAvailable, context.RequestAborted);
    }

    // 单次上游尝试：观测 → 转发 → 换发信号判定或收尾记录。
    private async Task<ForwardAttemptResult> ForwardOnceAsync(
        HttpContext context,
        UpstreamEndpoint upstream,
        AuthenticatedAccount account,
        DateTimeOffset startedAt,
        List<string> attemptedUpstreams,
        List<UpstreamSwitchReason> switchReasons)
    {
        HttpTransformer transformer = _pipelineFactory.CreateTransformer(upstream, _shared);
        using ResponseObservationScope observation = ResponseObservationScope.Begin(context, _observerFactory);

        ForwarderError error = await _forwarder.SendAsync(
            context,
            upstream.BaseAddress,
            _shared.HttpClient,
            _shared.RequestConfig,
            transformer,
            context.RequestAborted);

        if (ForwardingSignals.TryConsumeSwitchNext(context, out UpstreamSwitchReason? reason))
        {
            _upstreamSelector.Report(upstream, UpstreamOutcome.ExplicitFailure, DateTimeOffset.UtcNow);
            if (reason is not null)
            {
                switchReasons.Add(reason);
            }

            return ForwardAttemptResult.SwitchNext;
        }

        UsageSnapshot usage = observation.Complete();
        GatewayRequestLog entry = CreateRequestLog(
            context, upstream, account, error, usage, startedAt, attemptedUpstreams, switchReasons);
        await _logSink.WriteAsync(entry, CancellationToken.None);
        ApplyForwarderError(context, error);
        return ForwardAttemptResult.Completed;
    }

    // 组装元数据日志：换发路径与失败说明在此处汇入；失败说明写入前必须脱敏。
    private GatewayRequestLog CreateRequestLog(
        HttpContext context,
        UpstreamEndpoint upstream,
        AuthenticatedAccount account,
        ForwarderError error,
        UsageSnapshot usage,
        DateTimeOffset startedAt,
        List<string> attemptedUpstreams,
        List<UpstreamSwitchReason> switchReasons)
    {
        bool clientDisconnected = context.RequestAborted.IsCancellationRequested;
        bool gatewayError = error != ForwarderError.None;

        return new GatewayRequestLog
        {
            RequestId = context.TraceIdentifier,
            Timestamp = startedAt,
            Method = context.Request.Method,
            PathAndQuery = $"{context.Request.Path}{context.Request.QueryString}",
            StatusCode = context.Response.StatusCode,
            Elapsed = DateTimeOffset.UtcNow - startedAt,
            AccountId = account.Id,
            UpstreamId = upstream.Id,
            AttemptedUpstreams = attemptedUpstreams.ToArray(),
            SwitchReasons = switchReasons.ToArray(),
            Usage = usage,
            Outcome = RequestOutcomeClassifier.Classify(context.Response.StatusCode, clientDisconnected, gatewayError),
            FailureDetail = SecretRedactor.Redact(DescribeForwarderError(context, error), _secrets),
        };
    }

    private static string[] CollectSecrets(GatewayOptions options)
    {
        List<string> secrets = new(options.Accounts.Count + options.Upstreams.Count);
        foreach (AccountSettings account in options.Accounts)
        {
            secrets.Add(account.Key);
        }

        foreach (UpstreamSettings upstream in options.Upstreams)
        {
            secrets.Add(upstream.ApiKey);
        }

        return secrets.ToArray();
    }

    private static string? ExtractBearerToken(HttpContext context)
        => throw new NotImplementedException(); // TODO：读取 Authorization 头并去掉 "Bearer " 前缀；缺失返回 null

    private static string? DescribeForwarderError(HttpContext context, ForwarderError error)
        => throw new NotImplementedException(); // TODO：由转发错误特性取异常消息，无异常时按错误枚举给出简短说明

    private static void ApplyForwarderError(HttpContext context, ForwarderError error)
        => throw new NotImplementedException(); // TODO：未开始响应时 502/504；已开始则确保中断
}
