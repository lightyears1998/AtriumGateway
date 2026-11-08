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
    private const string BearerPrefix = "Bearer ";

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
            await TryWriteLogAsync(
                context,
                accountId: null,
                upstreamId: null,
                outcome: RequestOutcome.GatewayError,
                error: ForwarderError.None,
                usage: UsageSnapshot.Unknown,
                attemptedUpstreams: Array.Empty<string>(),
                switchReasons: Array.Empty<UpstreamSwitchReason>(),
                startedAt);
            return;
        }

        using RequestBodyBuffer bodyBuffer = await _bodyBufferFactory.CreateAsync(context, context.RequestAborted);
        ForwardingSignals.SetRequestBodyReplayable(context, bodyBuffer.IsReplayable);

        List<string> attemptedUpstreams = new();
        List<UpstreamSwitchReason> switchReasons = new();

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
                context, upstream, account, attemptedUpstreams, switchReasons, startedAt);

            if (attempt != ForwardAttemptResult.SwitchNext)
            {
                return;
            }
        }

        await _errorWriter.WriteAsync(context, GatewayErrorKind.NoUpstreamAvailable, context.RequestAborted);
        await WriteLogAsync(
            context,
            account.Id,
            attemptedUpstreams.Count > 0 ? attemptedUpstreams[^1] : null,
            outcome: RequestOutcome.GatewayError,
            error: ForwarderError.None,
            usage: UsageSnapshot.Unknown,
            attemptedUpstreams,
            switchReasons,
            startedAt);
    }

    // 单次上游尝试：观测 → 转发 → 换发信号判定或收尾记录。
    private async Task<ForwardAttemptResult> ForwardOnceAsync(
        HttpContext context,
        UpstreamEndpoint upstream,
        AuthenticatedAccount account,
        List<string> attemptedUpstreams,
        List<UpstreamSwitchReason> switchReasons,
        DateTimeOffset startedAt)
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

        _upstreamSelector.Report(upstream, UpstreamOutcome.PassThrough, DateTimeOffset.UtcNow);
        UsageSnapshot usage = observation.Complete();
        bool clientDisconnected = context.RequestAborted.IsCancellationRequested || IsClientCancellation(error);
        bool gatewayError = error != ForwarderError.None && !clientDisconnected;
        RequestOutcome outcome = RequestOutcomeClassifier.Classify(
            context.Response.StatusCode, clientDisconnected, gatewayError);

        await WriteLogAsync(
            context, account.Id, upstream.Id, outcome, error, usage, attemptedUpstreams, switchReasons, startedAt);

        return ForwardAttemptResult.Completed;
    }

    // 日志失败必须隔离降级，不得影响转发结果。
    private async Task TryWriteLogAsync(
        HttpContext context,
        string? accountId,
        string? upstreamId,
        RequestOutcome outcome,
        ForwarderError error,
        UsageSnapshot usage,
        IReadOnlyList<string> attemptedUpstreams,
        IReadOnlyList<UpstreamSwitchReason> switchReasons,
        DateTimeOffset startedAt)
    {
        try
        {
            await WriteLogAsync(
                context, accountId, upstreamId, outcome, error, usage, attemptedUpstreams, switchReasons, startedAt);
        }
        catch (Exception)
        {
            // 日志失败不影响已完成的转发。
        }
    }

    private async Task WriteLogAsync(
        HttpContext context,
        string? accountId,
        string? upstreamId,
        RequestOutcome outcome,
        ForwarderError error,
        UsageSnapshot usage,
        IReadOnlyList<string> attemptedUpstreams,
        IReadOnlyList<UpstreamSwitchReason> switchReasons,
        DateTimeOffset startedAt)
    {
        GatewayRequestLog entry = CreateRequestLog(
            context, accountId, upstreamId, outcome, error, usage, attemptedUpstreams, switchReasons, startedAt);
        await _logSink.WriteAsync(entry, CancellationToken.None);
    }

    private GatewayRequestLog CreateRequestLog(
        HttpContext context,
        string? accountId,
        string? upstreamId,
        RequestOutcome outcome,
        ForwarderError error,
        UsageSnapshot usage,
        IReadOnlyList<string> attemptedUpstreams,
        IReadOnlyList<UpstreamSwitchReason> switchReasons,
        DateTimeOffset startedAt)
    {
        string? contentType = context.Response.ContentType;
        bool isStreaming = contentType is not null
            && contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

        return new GatewayRequestLog
        {
            RequestId = context.TraceIdentifier,
            Timestamp = startedAt,
            Method = context.Request.Method,
            PathAndQuery = $"{context.Request.Path}{context.Request.QueryString}",
            StatusCode = context.Response.StatusCode,
            Elapsed = DateTimeOffset.UtcNow - startedAt,
            AccountId = accountId,
            UpstreamId = upstreamId,
            AttemptedUpstreams = attemptedUpstreams.ToArray(),
            SwitchReasons = switchReasons.ToArray(),
            Usage = usage,
            IsStreaming = isStreaming,
            Outcome = outcome,
            FailureDetail = SecretRedactor.Redact(DescribeForwarderError(context, error), _secrets),
        };
    }

    private static bool IsClientCancellation(ForwarderError error)
        => error is ForwarderError.RequestCanceled
            or ForwarderError.RequestBodyCanceled
            or ForwarderError.ResponseBodyCanceled
            or ForwarderError.UpgradeRequestCanceled
            or ForwarderError.UpgradeResponseCanceled;

    private static string? ExtractBearerToken(HttpContext context)
    {
        string header = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string token = header[BearerPrefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    private static string? DescribeForwarderError(HttpContext context, ForwarderError error)
    {
        if (error == ForwarderError.None)
        {
            return null;
        }

        IForwarderErrorFeature? feature = context.Features.Get<IForwarderErrorFeature>();
        Exception? exception = feature?.Exception;
        return exception is null
            ? error.ToString()
            : $"{error}: {exception.GetType().Name}: {exception.Message}";
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
}
