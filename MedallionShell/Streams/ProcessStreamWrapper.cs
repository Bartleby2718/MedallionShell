﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Medallion.Shell.PlatformCompatibilityHelper;

namespace Medallion.Shell.Streams
{
    /// <summary>
    /// On .NET Core and .NET Framework on Windows, writing to the standard input <see cref="Stream"/> after the process exits is a noop.
    /// 
    /// However, on Mono this will throw a "Write Fault" exception (https://github.com/madelson/MedallionShell/issues/6)
    /// while .NET Core on Linux throws a "Broken Pipe" exception (https://github.com/madelson/MedallionShell/issues/46).
    /// 
    /// This class wraps the underlying <see cref="Stream"/> to provide consistent behavior across platforms.
    /// </summary>
    internal sealed class ProcessStreamWrapper : Stream
    {
        private readonly Stream stream;

        private ProcessStreamWrapper(Stream stream) 
        {
            Debug.Assert(stream is not ProcessStreamWrapper, "No double wrapping");
            this.stream = stream;
        }

        public static Stream WrapIfNeeded(Stream stream, bool isReadStream) =>
            ProcessStreamsUseSyncIO || (!isReadStream && ProcessStreamWriteThrowsOnProcessEnd)
                ? new ProcessStreamWrapper(stream)
                : stream;

        public override bool CanRead => this.stream.CanRead;
        public override bool CanSeek => this.stream.CanSeek;
        public override bool CanWrite => this.stream.CanWrite;
        public override long Length => this.stream.Length;

        public override long Position { get => this.stream.Position; set => this.stream.Position = value; }

        public override bool CanTimeout => this.stream.CanTimeout;
        public override int ReadTimeout { get => this.stream.ReadTimeout; set => this.stream.ReadTimeout = value; }
        public override int WriteTimeout { get => this.stream.WriteTimeout; set => this.stream.WriteTimeout = value; }

        // Note: from my testing, try-catching on Flush() appears necessary with .NET core on Linux, but not on Mono
        public override void Flush() => 
            this.DoWriteOperation(default(bool), static (s, _) => s.Flush());
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            this.DoWriteOperationAsync(
                default(bool), 
                static (s, _, token) => s.FlushAsync(token),
                static (s, _) => s.Flush(),
                cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => 
            this.stream.Read(buffer, offset, count);

        public override int ReadByte() => base.ReadByte();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.DoIOAsync(
                (buffer, offset, count),
                static (s, arg, token) => s.ReadAsync(arg.buffer, arg.offset, arg.count, token),
                static (s, arg) => s.Read(arg.buffer, arg.offset, arg.count),
                cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => this.stream.Seek(offset, origin);

        public override void SetLength(long value) => this.stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            this.DoWriteOperation(
                (buffer, offset, count), 
                static (s, arg) => s.Write(arg.buffer, arg.offset, arg.count));

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.DoWriteOperationAsync(
                (buffer, offset, count),
                static (s, arg, token) => s.WriteAsync(arg.buffer, arg.offset, arg.count, token),
                static (s, arg) => s.Write(arg.buffer, arg.offset, arg.count),
                cancellationToken);

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1
        public override int Read(Span<byte> buffer) => this.stream.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            new(this.DoIOAsync(
                buffer,
                static (s, buffer, token) => s.ReadAsync(buffer, token).AsTask(),
                static (s, buffer) => s.Read(buffer.Span),
                cancellationToken));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // NOTE: we can't use DoWriteOperation here because ReadOnlySpan cannot be a generic argument

            try { this.stream.Write(buffer); }
            catch (IOException) when (ProcessStreamWriteThrowsOnProcessEnd) { }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            new(this.DoWriteOperationAsync(
                buffer,
                static (s, buffer, token) => s.WriteAsync(buffer, token).AsTask(),
                static (s, buffer) => s.Write(buffer.Span),
                cancellationToken));
#endif

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (!ProcessStreamsUseSyncIO) // can short-circuit since base.CopyToAsync() still calls our Read
            {
                base.CopyToAsync(destination, bufferSize, cancellationToken);
            }

#if NET6_0_OR_GREATER
            ValidateCopyToArguments(destination, bufferSize);
#else
            Throw.IfNull(destination, nameof(destination));
            if (bufferSize <= 0) { throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Must be positive"); }
            if (!destination.CanWrite)
            {
                if (destination.CanRead) { throw new NotSupportedException("Unwritable stream"); }

                throw new ObjectDisposedException(destination.GetType().ToString(), "Cannot access a closed Stream");
            }
#endif

            if (!this.CanRead)
            {
                if (!this.CanWrite) { throw new NotSupportedException("Unreadable stream"); }

                throw new ObjectDisposedException(this.GetType().ToString(), "Cannot access a closed stream");
            }

            return CopyToAsyncHelper(this, destination, bufferSize, cancellationToken);

            static async Task CopyToAsyncHelper(Stream source, Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                using var operation = BeginMultiStepIOOperation(source, destination);

                var buffer = new byte[bufferSize];
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private void DoWriteOperation<TArg>(TArg arg, Action<Stream, TArg> action)
        {
            // NOTE: keep in sync with Write(ReadOnlySpan)

            try { action(this.stream, arg); }
            // this approach is a bit risky since we might end up suppressing other unrelated IO exceptions.
            // However, aside from trying to check the process (which brings its own risks if the process is in
            // a weird state), there is no really robust way to do this given that different OS's yield different
            // error messages
            catch (IOException) when (ProcessStreamWriteThrowsOnProcessEnd) { }
        }

        private async Task DoWriteOperationAsync<TArg>(
            TArg arg,
            Func<Stream, TArg, CancellationToken, Task> asyncAction,
            Action<Stream, TArg> syncAction, 
            CancellationToken cancellationToken)
        {
            try 
            {
                await this.DoIOAsync(
                    (arg, syncAction, asyncAction),
                    static async (stream, arg, token) =>
                    {
                        await arg.asyncAction(stream, arg.arg, token).ConfigureAwait(false);
                        return false;
                    },
                    static (stream, arg) =>
                    {
                        arg.syncAction(stream, arg.arg);
                        return false;
                    },
                    cancellationToken
                ).ConfigureAwait(false);
            }
            // see comment in Write()
            catch (IOException) when (ProcessStreamWriteThrowsOnProcessEnd) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.stream.Dispose();
            }
            base.Dispose(disposing);
        }

        private Task<TResult> DoIOAsync<TArg, TResult>(
            TArg arg, 
            Func<Stream, TArg, CancellationToken, Task<TResult>> asyncFunc, 
            Func<Stream, TArg, TResult> syncFunc, 
            CancellationToken cancellationToken)
        {
            if (!ProcessStreamsUseSyncIO)
            {
                return asyncFunc(this.stream, arg, cancellationToken);
            }

            var syncIOOperation = SyncIOOperation.Current;
            if (syncIOOperation?.Contains(this) ?? false)
            {
                // go fully synchronous if we can to avoid spinning up new threads
                if (syncIOOperation.IsSyncIOAllowedOnCurrentThread)
                {
                    if (cancellationToken.IsCancellationRequested) { return Shims.CanceledTask<TResult>(cancellationToken); }
                    try { return Task.FromResult(syncFunc(this.stream, arg)); }
                    catch (Exception ex) { return Shims.FaultedTask<TResult>(ex); }
                }
            }
            else
            {
                syncIOOperation = null; // current operation is not relevant
            }

            // avoid doing async IO on the threadpool, which can lead to starvation
            return Task.Factory.StartNew(
                static state =>
                {
                    var tupleState = (Tuple<Stream, TArg, Func<Stream, TArg, TResult>, SyncIOOperation?>)state!;
                    tupleState.Item4?.MarkCurrentThreadForSyncIO();
                    return tupleState.Item3(tupleState.Item1, tupleState.Item2);
                },
                state: Tuple.Create(this.stream, arg, syncFunc, syncIOOperation),
                cancellationToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default
            );
        }

        public static IDisposable? BeginMultiStepIOOperation(Stream? stream1, Stream? stream2 = null) =>
            ProcessStreamsUseSyncIO ? SyncIOOperation.Start(stream1, stream2) : null;

        private sealed class SyncIOOperation : IDisposable
        {
            private static readonly
#if DEBUG && NETFRAMEWORK // see testing comment in AsyncLocalShim.cs
                Medallion.Shell.
#endif
                AsyncLocal<SyncIOOperation?> AsyncLocalCurrent = new();

            [ThreadStatic]
            private static SyncIOOperation? threadLocalCurrent;

            private SyncIOOperation? previous;
            private ProcessStreamWrapper? stream1, stream2;
            private bool disposed;

            private SyncIOOperation() { }

            public static SyncIOOperation? Current => AsyncLocalCurrent.Value;

            public static SyncIOOperation? Start(Stream? stream1, Stream? stream2)
            {
                var wrapperStream1 = stream1 as ProcessStreamWrapper;
                var wrapperStream2 = stream2 as ProcessStreamWrapper;

                if (wrapperStream1 is null && wrapperStream2 is null) { return null; }

                // If we already have an equivalent current operation, we don't want to overwrite it.
                // Instead, we simply return null so that it can be used without being disposed an extra time.
                var current = AsyncLocalCurrent.Value;
                if (current != null
                    && (stream1 is null || current.Contains(stream1))
                    && (stream2 is null || current.Contains(stream2)))
                {
                    return null;
                }

                return AsyncLocalCurrent.Value = new() { stream1 = wrapperStream1, stream2 = wrapperStream2, previous = AsyncLocalCurrent.Value };
            }

            public bool IsSyncIOAllowedOnCurrentThread => threadLocalCurrent == this;

            public void MarkCurrentThreadForSyncIO()
            {
                Debug.Assert(threadLocalCurrent is null, "must be a new thread");
#if !NETSTANDARD1_3
                Debug.Assert(!Thread.CurrentThread.IsThreadPoolThread, "must not be a threadpool thread");
#endif
                if (!this.disposed) { threadLocalCurrent = this; } 
            }

            public bool Contains(Stream stream) =>
                this.stream1 == stream || this.stream2 == stream;

            public void Dispose()
            {
                if (this.disposed) { return; }
                this.disposed = true;

                if (threadLocalCurrent == this)
                {
                    threadLocalCurrent = null;
                }
                if (AsyncLocalCurrent.Value == this)
                {
                    AsyncLocalCurrent.Value = this.previous;
                }

                this.stream1 = this.stream2 = null;
                this.previous = null;
            }
        }
    }
}
