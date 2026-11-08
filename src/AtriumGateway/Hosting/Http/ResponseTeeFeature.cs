using System.IO.Pipelines;
using AtriumGateway.Core.Observation;
using Microsoft.AspNetCore.Http.Features;

namespace AtriumGateway.Hosting.Http;

/// <summary>响应体 feature 的 tee 包装：只用观察流替换写路径，其余能力委托原始 feature。</summary>
internal sealed class ResponseTeeFeature : IHttpResponseBodyFeature
{
    private readonly IHttpResponseBodyFeature _inner;
    private readonly IResponseObserver _observer;
    private ResponseTeeStream? _stream;
    private PipeWriter? _writer;

    public ResponseTeeFeature(IHttpResponseBodyFeature inner, IResponseObserver observer)
    {
        _inner = inner;
        _observer = observer;
    }

    public Stream Stream => _stream ??= new ResponseTeeStream(_inner.Stream, _observer);

    public PipeWriter Writer => _writer ??= PipeWriter.Create(Stream, new StreamPipeWriterOptions(leaveOpen: true));

    public Task StartAsync(CancellationToken cancellationToken) => _inner.StartAsync(cancellationToken);

    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：YARP 不使用 SendFile；不得提供绕过 tee 的路径

    public Task CompleteAsync() => _inner.CompleteAsync();

    public void DisableBuffering() => _inner.DisableBuffering();
}
