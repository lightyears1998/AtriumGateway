using System.Text.Json;

namespace AtriumGateway.Core.Observation;

/// <summary>SSE 逐行观察器：只读识别 <c>data:</c> 事件中的 <c>model</c> 与 <c>usage</c>。</summary>
internal sealed class SseUsageObserver : IResponseObserver
{
    private readonly int _maxBufferedBytes;
    private byte[] _buffer = new byte[512];
    private int _count;
    private bool _aborted;
    private bool _sawEvent;
    private bool _hasUsage;
    private string? _model;
    private TokenUsage _usage = TokenUsage.Empty;

    public SseUsageObserver(int maxBufferedBytes) => _maxBufferedBytes = maxBufferedBytes;

    public void Observe(ReadOnlySpan<byte> chunk)
    {
        if (_aborted || chunk.IsEmpty)
        {
            return;
        }

        if (_count + chunk.Length > _maxBufferedBytes)
        {
            _aborted = true;
            _count = 0;
            return;
        }

        EnsureCapacity(_count + chunk.Length);
        chunk.CopyTo(_buffer.AsSpan(_count));
        _count += chunk.Length;
        ProcessLines();
    }

    public UsageSnapshot Complete()
    {
        if (_aborted)
        {
            return new UsageSnapshot(_model, TokenUsage.Empty, UsageAvailability.ObservationAborted);
        }

        if (_count > 0)
        {
            ProcessLine(_buffer.AsSpan(0, _count));
            _count = 0;
        }

        if (_hasUsage)
        {
            return new UsageSnapshot(_model, _usage, UsageAvailability.Parsed);
        }

        return _sawEvent
            ? new UsageSnapshot(_model, TokenUsage.Empty, UsageAvailability.UpstreamNotReported)
            : UsageSnapshot.Unknown;
    }

    private void ProcessLines()
    {
        int start = 0;
        while (true)
        {
            int newline = _buffer.AsSpan(start, _count - start).IndexOf((byte)'\n');
            if (newline < 0)
            {
                break;
            }

            int end = start + newline;
            ProcessLine(_buffer.AsSpan(start, end - start));
            start = end + 1;
        }

        if (start == 0)
        {
            return;
        }

        int remaining = _count - start;
        if (remaining > 0)
        {
            _buffer.AsSpan(start, remaining).CopyTo(_buffer);
        }

        _count = remaining;
    }

    private void ProcessLine(ReadOnlySpan<byte> line)
    {
        ReadOnlySpan<byte> trimmed = ByteLine.Trim(line);
        if (trimmed.IsEmpty || trimmed[0] == (byte)':')
        {
            return;
        }

        ReadOnlySpan<byte> dataPrefix = "data:"u8;
        if (!trimmed.StartsWith(dataPrefix))
        {
            return;
        }

        ReadOnlySpan<byte> payload = ByteLine.Trim(trimmed[dataPrefix.Length..]);
        if (payload.IsEmpty || payload.SequenceEqual("[DONE]"u8))
        {
            return;
        }

        _sawEvent = true;
        if (!UsagePayloadParser.TryParse(payload, out string? model, out TokenUsage usage, out bool hasUsage))
        {
            return;
        }

        if (model is not null)
        {
            _model = model;
        }

        if (hasUsage)
        {
            _usage = usage;
            _hasUsage = true;
        }
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        int size = _buffer.Length;
        while (size < required)
        {
            size *= 2;
        }

        Array.Resize(ref _buffer, size);
    }
}

/// <summary>非流式 JSON 观察器：在上限内缓冲，结束后提取 <c>model</c> 与 <c>usage</c>。</summary>
internal sealed class JsonUsageObserver : IResponseObserver
{
    private readonly int _maxBufferedBytes;
    private byte[] _buffer = new byte[4096];
    private int _count;
    private bool _aborted;

    public JsonUsageObserver(int maxBufferedBytes) => _maxBufferedBytes = maxBufferedBytes;

    public void Observe(ReadOnlySpan<byte> chunk)
    {
        if (_aborted || chunk.IsEmpty)
        {
            return;
        }

        if (_count + chunk.Length > _maxBufferedBytes)
        {
            _aborted = true;
            return;
        }

        EnsureCapacity(_count + chunk.Length);
        chunk.CopyTo(_buffer.AsSpan(_count));
        _count += chunk.Length;
    }

    public UsageSnapshot Complete()
    {
        if (_aborted)
        {
            return new UsageSnapshot(null, TokenUsage.Empty, UsageAvailability.ObservationAborted);
        }

        if (!UsagePayloadParser.TryParse(_buffer.AsSpan(0, _count), out string? model, out TokenUsage usage, out bool hasUsage))
        {
            return UsageSnapshot.Unknown;
        }

        return hasUsage
            ? new UsageSnapshot(model, usage, UsageAvailability.Parsed)
            : new UsageSnapshot(model, TokenUsage.Empty, UsageAvailability.UpstreamNotReported);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        int size = _buffer.Length;
        while (size < required)
        {
            size *= 2;
        }

        Array.Resize(ref _buffer, size);
    }
}

/// <summary>压缩响应：不解压，直接标记为不可观测。</summary>
internal sealed class CompressedResponseObserver : IResponseObserver
{
    public void Observe(ReadOnlySpan<byte> chunk)
    {
        // 不读取压缩字节。
    }

    public UsageSnapshot Complete()
        => new(null, TokenUsage.Empty, UsageAvailability.CompressedResponse);
}

/// <summary>无法识别的响应：不做观测。</summary>
internal sealed class UnsupportedResponseObserver : IResponseObserver
{
    public void Observe(ReadOnlySpan<byte> chunk)
    {
        // 不读取。
    }

    public UsageSnapshot Complete() => UsageSnapshot.Unknown;
}

/// <summary>从 OpenAI 风格 JSON 中只读提取 <c>model</c> 与 <c>usage</c>。</summary>
internal static class UsagePayloadParser
{
    public static bool TryParse(ReadOnlySpan<byte> json, out string? model, out TokenUsage usage, out bool hasUsage)
    {
        model = null;
        usage = TokenUsage.Empty;
        hasUsage = false;

        if (json.IsEmpty)
        {
            return false;
        }

        try
        {
            Utf8JsonReader reader = new(json, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (reader.ValueTextEquals("model"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String)
                    {
                        model = reader.GetString();
                    }

                    continue;
                }

                if (reader.ValueTextEquals("usage"u8) && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                {
                    usage = ReadUsage(ref reader);
                    hasUsage = true;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TokenUsage ReadUsage(ref Utf8JsonReader reader)
    {
        int? prompt = null;
        int? completion = null;
        int? total = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("prompt_tokens"u8))
            {
                prompt = ReadInt32(ref reader);
            }
            else if (reader.ValueTextEquals("completion_tokens"u8))
            {
                completion = ReadInt32(ref reader);
            }
            else if (reader.ValueTextEquals("total_tokens"u8))
            {
                total = ReadInt32(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return new TokenUsage(prompt, completion, total);
    }

    private static int? ReadInt32(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            return null;
        }

        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value) ? value : null;
    }

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }
    }
}

/// <summary>行内空白裁剪（字节级，避免依赖 Span.Trim 的字节重载）。</summary>
internal static class ByteLine
{
    public static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> line)
    {
        int start = 0;
        int end = line.Length - 1;

        while (start <= end && IsWhitespace(line[start]))
        {
            start++;
        }

        while (end >= start && IsWhitespace(line[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<byte>.Empty : line[start..(end + 1)];
    }

    private static bool IsWhitespace(byte value)
        => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
