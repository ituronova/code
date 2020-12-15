using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    class ConcatenatedStream : Stream
    {
        List<Stream> streams;
        int current = 0;

        public ConcatenatedStream(IEnumerable<Stream> streams)
        {
            this.streams = new List<Stream>(streams);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => streams.Sum(s => s.Length);

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (current == streams.Count)
            {
                return 0;
            }

            int bytesRead = streams[current].Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                current += 1;
                if (current < streams.Count) streams[current].Seek(0, SeekOrigin.Begin);
                bytesRead += Read(buffer, offset + bytesRead, count - bytesRead);
            }
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    current = 0;
                    var subOffset = offset;
                    while (subOffset >= streams[current].Length)
                    {
                        subOffset -= streams[current].Length;
                        current += 1;
                    }
                    streams[current].Seek(subOffset, SeekOrigin.Begin);
                    return offset;
                default:
                    throw new NotImplementedException("Only SeekOrigin.Begin is implemented");
            }
        }

        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public override void Flush() { throw new NotImplementedException(); }

        public override void SetLength(long value) { throw new NotImplementedException(); }

        public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }

        protected override void Dispose(bool disposing)
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }
}
