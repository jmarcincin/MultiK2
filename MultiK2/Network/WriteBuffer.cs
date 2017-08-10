using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    internal class WriteBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _maxPacketSize;
        private int _packetStartOffset;

        private int _readOffset;

        private int _writeOffset;
        
        public int RemainingPacketWriteCapacity => _maxPacketSize - (_writeOffset - _packetStartOffset);

        public int ReadOffset => _readOffset;

        public int WriteOffset => _writeOffset;

        public bool FinalizedPacket { get; private set; }

        /// <summary>
        /// Network data reader / writer helper class.
        /// </summary>
        /// <param name="bufferSize">Maximum capacity of network buffer.</param>
        public WriteBuffer(int maxPacketSize)
        {
            _buffer = new byte[maxPacketSize];
            _maxPacketSize = maxPacketSize;
        }

        public byte[] GetBuffer()
        {
            return _buffer;
        }

        public void StartPacket()
        {
            // todo: append for parallel processing / or simple reset?
            _writeOffset = _readOffset = 0;
            _packetStartOffset = _writeOffset;
            FinalizedPacket = false;

            // reserve space for size header
            _writeOffset += 4;
        }

        public void FinalizePacket()
        {
            var packetSize = _writeOffset - _packetStartOffset;

            // todo sanity checking W > R & max packet size

            _buffer[_packetStartOffset] = (byte)packetSize;
            _buffer[_packetStartOffset + 1] = (byte)(packetSize >> 8);

            // max packet size currently restricted to ushort max >> another 2 bytes used as primitive way
            // of the stream corruption or read / write algorithm error detection
            _buffer[_packetStartOffset + 2] = (byte)(packetSize >> 16);
            _buffer[_packetStartOffset + 3] = (byte)(packetSize >> 24);

            FinalizedPacket = true;
        }
        
        public bool ReserveForWrite(int requestedSize, out int offset)
        {
            if (_writeOffset + requestedSize <= _buffer.Length)
            {
                offset = _writeOffset;
                _writeOffset += requestedSize;
                return true;
            }
            offset = -1;
            return false;
        }

        public void Write(int value)
        {
            _buffer[_writeOffset] = (byte)value;
            _buffer[_writeOffset + 1] = (byte)(value >> 8);
            _buffer[_writeOffset + 2] = (byte)(value >> 16);
            _buffer[_writeOffset + 3] = (byte)(value >> 24);
            _writeOffset += 4;
        }

        public void Write(long value)
        {
            _buffer[_writeOffset] = (byte)value;
            _buffer[_writeOffset + 1] = (byte)(value >> 8);
            _buffer[_writeOffset + 2] = (byte)(value >> 16);
            _buffer[_writeOffset + 3] = (byte)(value >> 24);
            _buffer[_writeOffset + 4] = (byte)(value >> 32);
            _buffer[_writeOffset + 5] = (byte)(value >> 40);
            _buffer[_writeOffset + 6] = (byte)(value >> 48);
            _buffer[_writeOffset + 7] = (byte)(value >> 56);
            _writeOffset += 8;
        }

        public void Write(float value)
        {
            // TODO: evaluate alternative - union(struct) non-heap allocation conversion
            unsafe
            {
                float* ptr = stackalloc float[1];
                ptr[0] = value;
                int* intPtr = (int*)ptr;
                Write(*intPtr);
            }
        }        
    }
}
