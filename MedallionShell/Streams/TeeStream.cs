using System;
using System.IO;

namespace Medallion.Shell.Streams
{
    /// <summary>
    /// TODO:
    /// </summary>
    internal class TeeStream : Stream
    {
        private readonly Stream _stream1;
        private readonly Stream _stream2;

        public TeeStream(Stream stream1, Stream stream2)
        {
            this._stream1 = stream1;
            this._stream2 = stream2;
        }
        public override bool CanRead => this._stream1.CanRead && this._stream2.CanRead;

        public override bool CanSeek => this._stream1.CanSeek && this._stream2.CanSeek;

        public override bool CanWrite => this._stream1.CanWrite && this._stream2.CanWrite;

        // TODO:
        public override long Length => Math.Max(this._stream1.Length, this._stream2.Length);

        public override long Position
        {
            get
            {
                if (this._stream1.Position != this._stream2.Position)
                {
                    throw new InvalidOperationException("TODO:");
                }
                return this._stream1.Position;
            }
            set
            {
                this._stream1.Position = value;
                this._stream2.Position = value;
            }
        }

        public override void Flush()
        {
            this._stream1.Flush();
            this._stream2.Flush();
        }

        // TODO:
        public override int Read(byte[] buffer, int offset, int count) => this._stream1.Read(buffer, offset, count);

        // TODO:
        public override long Seek(long offset, SeekOrigin origin) => this._stream1.Seek(offset, origin);

        public override void SetLength(long value)
        {
            this._stream1.SetLength(value);
            this._stream2.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._stream1.Write(buffer, offset, count);
            this._stream2.Write(buffer, offset, count);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1
        public override int Read(Span<byte> buffer) => throw new NotImplementedException();

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotImplementedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotImplementedException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotImplementedException();
#endif
    }
}
