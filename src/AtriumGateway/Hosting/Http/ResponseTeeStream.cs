using AtriumGateway.Core.Observation;

namespace AtriumGateway.Hosting.Http;

/// <summary>写入路径上的 tee：先喂给只读观察器，再原样写入下游并按下游语义 flush。</summary>
internal sealed class ResponseTeeStream : Stream
{
    private readonly Stream _inner;
    private readonly IResponseObserver _observer;

    public ResponseTeeStream(Stream inner, IResponseObserver observer)
    {
        _inner = inner;
        _observer = observer;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
        => WriteAndObserve(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
        => WriteAndObserve(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => WriteAndObserveAsync(buffer, cancellationToken);

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private void WriteAndObserve(ReadOnlySpan<byte> buffer)
        => throw new NotImplementedException(); // TODO：_observer.Observe(buffer) 后 _inner.Write(buffer)

    private ValueTask WriteAndObserveAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        => throw new NotImplementedException(); // TODO：_observer.Observe 后 _inner.WriteAsync（下游按块 flush）
}
