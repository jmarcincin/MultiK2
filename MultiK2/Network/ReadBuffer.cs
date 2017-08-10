using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    internal class ReadBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _maxPacketSize;

        private int _packetStartOffset;

        private int _readOffset;

        private int _writeOffset;

        private int _expectedSize;

        public int RemainingPacketWriteCapacity => ushort.MaxValue - (_writeOffset - _packetStartOffset);

        public int ReadOffset => _readOffset;

        public int WriteOffset => _writeOffset;

        /// <summary>
        /// Network data reader / writer helper class.
        /// </summary>
        /// <param name="bufferSize">Maximum capacity of network buffer.</param>
        public ReadBuffer(int maxPacketSize)
        {
            _buffer = new byte[maxPacketSize * 2];
            _maxPacketSize = maxPacketSize;
        }

        public byte[] GetBuffer()
        {
            return _buffer;
        }

        public void UpdateWritePointer(int byteCount)
        {
            _writeOffset += byteCount;
        }

        public bool IsPacketReceiveCompleted(out int remainingSize)
        {
            if (_expectedSize == 0)
            {
                if (_writeOffset >= _readOffset + 4)
                {
                    _packetStartOffset = _readOffset;

                    // TODO: header corruption r /w error check & max size
                    _expectedSize = ReadInt32();

                    // return read offset to beginning of the packet
                    _readOffset -= 4;
                }
                else
                {
                    remainingSize = _maxPacketSize;
                    return false;
                }
            }

            // we need to add size header back for size eval
            var receivedSize = _writeOffset - _readOffset;
            if (receivedSize >= _expectedSize)
            {
                // skip size header
                _readOffset += 4;
                remainingSize = 0;
                return true;
            }

            remainingSize = _expectedSize - receivedSize;
            return false;
        }

        public void EndReadingPacket()
        {
            _readOffset = _packetStartOffset + _expectedSize;
            _expectedSize = 0;
        }

        //todo split - skipping method + reset method
        public void ResetBuffer()
        {
            // reset pointers  / buffer if there is not enough place for max size packet remaining
            var remainingCapacity = _buffer.Length - _writeOffset;
            var unprocessedBytesCount = _writeOffset - _readOffset;

            if (unprocessedBytesCount > 0)
            {                
                /*
                if (remainingCapacity < _maxPacketSize)
                {
                    // move data in buffer to the beginning
                    Array.Copy(_buffer, _readOffset, _buffer, 0, notReadCount);
                    _packetStartOffset = 0;
                    _readOffset = 0;
                    _writeOffset = notReadCount;
                }*/
            }
            else
            {
                _packetStartOffset = 0;
                _readOffset = 0;
                _writeOffset = 0;
            }
        }

        public bool ReserveForReading(int requestedSize, out int offset)
        {
            // TODO: checking vs writeoffset?
            if (_readOffset + requestedSize <= _buffer.Length)
            {
                offset = _readOffset;
                _readOffset += requestedSize;
                return true;
            }
            offset = -1;
            return false;
        }

        public int ReadInt32()
        {
            // TODO: data presence check in debug?
            var result = _buffer[_readOffset] |
                        (_buffer[_readOffset + 1] << 8) |
                        (_buffer[_readOffset + 2] << 16) |
                        (_buffer[_readOffset + 3] << 24);
            _readOffset += 4;
            return result;
        }

        public long ReadInt64()
        {
            // TODO: data presence check in debug?
            var result = _buffer[_readOffset] |
                        ((long)_buffer[_readOffset + 1] << 8) |
                        ((long)_buffer[_readOffset + 2] << 16) |
                        ((long)_buffer[_readOffset + 3] << 24) |
                        ((long)_buffer[_readOffset + 4] << 32) |
                        ((long)_buffer[_readOffset + 5] << 40) |
                        ((long)_buffer[_readOffset + 6] << 48) |
                        ((long)_buffer[_readOffset + 7] << 56);
            _readOffset += 8;
            return result;
        }

        public float ReadSingle()
        {
            // TODO: evaluate alternative - union(struct) non-heap allocation conversion
            unsafe
            {
                int* ptr = stackalloc int[1];
                ptr[0] = ReadInt32();
                float* floatPtr = (float*)ptr;
                return *floatPtr;
            }
        }        
    }
}
