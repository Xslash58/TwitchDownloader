﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    // Modified from https://stackoverflow.com/a/32724000
    public class ThrottledStream : Stream
    {
        public readonly Stream BaseStream;
        public readonly int MaximumBytesPerSecond;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _totalBytesRead = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottledStream"/> class
        /// </summary>
        /// <param name="in">The base stream to be read from in a throttled manner</param>
        /// <param name="throttleKb">The maximum read bandwidth in kilobytes per second</param>
        public ThrottledStream(Stream @in, int throttleKb)
        {
            const int FOURTY_MEGABYTES = 40_960;
            MaximumBytesPerSecond = Math.Min(throttleKb, FOURTY_MEGABYTES) * 1024;
            BaseStream = @in;
        }

        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => false;

        public override void Flush() { }

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var newCount = GetBytesToReturn(count);
            var read = BaseStream.Read(buffer, offset, newCount);
            Interlocked.Add(ref _totalBytesRead, read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value) { }

        public override void Write(byte[] buffer, int offset, int count) { }

        private int GetBytesToReturn(int count)
        {
            return GetBytesToReturnAsync(count).GetAwaiter().GetResult();
        }

        private async Task<int> GetBytesToReturnAsync(int count)
        {
            if (MaximumBytesPerSecond <= 0)
                return count;

            var canSend = (long)(_watch.ElapsedMilliseconds * (MaximumBytesPerSecond / 1000.0));

            var diff = (int)(canSend - _totalBytesRead);

            if (diff <= 0)
            {
                var waitInSec = ((diff * -1.0) / (MaximumBytesPerSecond));

                await Task.Delay((int)(waitInSec * 1000)).ConfigureAwait(false);
            }

            if (diff >= count) return count;

            return diff > 0 ? diff : Math.Min(1024 * 8, count);
        }
    }
}