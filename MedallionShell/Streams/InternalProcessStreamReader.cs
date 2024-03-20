using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Shell.Streams
{
    internal sealed class InternalProcessStreamReader : ProcessStreamReader
    {
        /// <summary>
        /// The underlying <see cref="Stream"/> from the <see cref="Process"/>
        /// </summary>
        private readonly Stream processStream;
        private readonly Pipe pipe;
        private readonly StreamReader reader;
        private readonly TextWriter? additionalOutput;
        private volatile bool discardContents;

        public InternalProcessStreamReader(StreamReader processStreamReader, TextWriter? additionalOutput = null)
        {
            this.processStream = ProcessStreamWrapper.WrapIfNeeded(processStreamReader.BaseStream, isReadStream: true);
            this.pipe = new Pipe();
            this.reader = new StreamReader(this.pipe.OutputStream, processStreamReader.CurrentEncoding);
            this.Task = this.BufferLoopAsync();
            this.additionalOutput = additionalOutput;
        }

        public Task Task { get; }

        private async Task BufferLoopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var buffer = new byte[Constants.ByteBufferSize];
                while (
                    !this.discardContents
                        && await this.processStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false) is int bytesRead && bytesRead > 0)
                {
                    await this.pipe.InputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                this.processStream.Dispose();
                this.pipe.InputStream.Dispose();
            }
        }

#region ---- ProcessStreamReader implementation ----
        public override Stream BaseStream => this.reader.BaseStream;

        public override Encoding Encoding => this.reader.CurrentEncoding;

        public override TextWriter? AdditionalOutput => this.additionalOutput;

        public override void Discard()
        {
            this.discardContents = true;
            this.reader.Dispose();
        }

        public override void StopBuffering()
        {
            // this causes writes to the pipe to block, thus
            // preventing unbounded buffering (although some more content
            // may still be buffered)
            this.pipe.SetFixedLength();
        }
#endregion

#region ---- TextReader implementation ----
        // all reader methods are overriden to call the same method on the underlying StreamReader.
        // This approach is preferable to extending StreamReader directly, since many of the async methods
        // on StreamReader are conservative and fall back to threaded asynchrony when inheritance is in play
        // (this is done to respect any overriden Read() call). This way, we get the full benefit of asynchrony.

        public override int Peek()
        {
            return this.reader.Peek();
        }

        public override int Read()
        {
            var readData = this.reader.Read();
            this.additionalOutput?.Write(readData);
            return readData;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var readData = this.reader.Read(buffer, index, count);
            this.additionalOutput?.Write(buffer, index, count);
            return readData;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            var readData = await this.reader.ReadAsync(buffer, index, count).ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteAsync(buffer, index, count).ConfigureAwait(false); }
            return readData;
        }

        public override int ReadBlock(char[] buffer, int index, int count)
        {
            var readBytesCount = this.reader.ReadBlock(buffer, index, count);
            this.additionalOutput?.Write(buffer, index, count);
            return readBytesCount;
        }

        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            var readBytes = await this.reader.ReadBlockAsync(buffer, index, count).ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteAsync(buffer, index, count).ConfigureAwait(false); }
            return readBytes;
        }

        public override string? ReadLine()
        {
            var readData = this.reader.ReadLine();
            this.additionalOutput?.Write(readData);
            return readData;
        }

        public override async Task<string?> ReadLineAsync()
        {
            var readData = await this.reader.ReadLineAsync().ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteLineAsync(readData).ConfigureAwait(false); }
            return readData;
        }

        public override string ReadToEnd()
        {
            var readData = this.reader.ReadToEnd();
            this.additionalOutput?.Write(readData);
            return readData;
        }

        public override async Task<string> ReadToEndAsync()
        {
            var readData = await this.reader.ReadToEndAsync().ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteLineAsync(readData).ConfigureAwait(false); }
            return readData;
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1
        public override int Read(Span<char> buffer)
        {
            var readChartsCount = this.reader.Read(buffer);
            this.additionalOutput?.Write(buffer);
            return readChartsCount;
        }

        public override int ReadBlock(Span<char> buffer)
        {
            var readChartsCount = this.reader.ReadBlock(buffer);
            this.additionalOutput?.Write(buffer);
            return readChartsCount;
        }

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken)
        {
            var readChartsCount = await this.reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteLineAsync(buffer, cancellationToken).ConfigureAwait(false); }
            return readChartsCount;
        }

        public override async ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken)
        {
            var readChartsCount = await this.reader.ReadBlockAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (this.additionalOutput is { } additionalOutput) { await additionalOutput.WriteLineAsync(buffer, cancellationToken).ConfigureAwait(false); }
            return readChartsCount;
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Discard();
                this.additionalOutput?.Dispose();
            }
        }
#endregion
    }
}
