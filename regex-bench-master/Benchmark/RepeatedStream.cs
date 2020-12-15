using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    class RepeatedStream : Stream
    {
        private Stream stream;
        private int current = 0;
        private int repeat;

        public RepeatedStream(Stream stream, int count)
        {
            this.stream = stream;
            this.repeat = count;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => stream.Length * repeat;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (current == repeat)
            {
                return 0;
            }

            int bytesRead = stream.Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
                current += 1;
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
                    while (subOffset >= stream.Length)
                    {
                        subOffset -= stream.Length;
                        current += 1;
                    }
                    stream.Seek(subOffset, SeekOrigin.Begin);
                    return offset;
                default:
                    throw new NotImplementedException("Only SeekOrigin.Begin is implemented");
            }
        }

        public override long Position { get { throw new NotImplementedException(); }  set { throw new NotImplementedException(); } }

        public override void Flush() { throw new NotImplementedException(); }

        public override void SetLength(long value) { throw new NotImplementedException(); }

        public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
    }
}
