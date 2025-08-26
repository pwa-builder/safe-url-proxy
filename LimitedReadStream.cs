using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PWABuilder.SafeUrl;

/// <summary>
/// A stream that reads from an underlying stream but limits the total number of bytes that can be read. It also includes the media type header.
/// </summary>
public class LimitedReadStream : Stream
{
    private readonly Stream inner;
    private readonly string? mediaType;
    private readonly long maxBytes;
    private long totalReadBytes;

    public LimitedReadStream(Stream inner, long maxBytes, string? mediaType)
    {
        this.inner = inner;
        this.maxBytes = maxBytes;
        this.mediaType = mediaType;
    }

    /// <summary>
    /// Gets the media type of the underlying image stream.
    /// </summary>
    public string? MediaType => mediaType;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (totalReadBytes >= maxBytes)
        {
            throw new IOException("Stream exceeds maximum allowed size.");
        }

        var toRead = (int)Math.Min(count, maxBytes - totalReadBytes);
        var read = inner.Read(buffer, offset, toRead);
        totalReadBytes += read;
        return read;
    }

    // Implement required overrides
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
