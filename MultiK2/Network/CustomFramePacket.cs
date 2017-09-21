using System;
using System.Numerics;
using Windows.Graphics.Imaging;
using MultiK2.Utils;

namespace MultiK2.Network
{
    internal class CustomFramePacket : FramePacket
    {        
        private int _offset;
        private bool _init = true;

        public byte[] Data { get; private set; }
        
        public CustomFramePacket(byte[] dataToSend) : base(ReaderType.UserDefined)
        {
            Data = dataToSend;
        }

        public CustomFramePacket() : base(ReaderType.UserDefined)
        {
        }

        public override bool WriteData(WriteBuffer writer)
        {
            if (_init)
            {
                writer.Write((int)OperationCode.UserFrameTransfer);
                writer.Write((int)OperationStatus.PushInit);
                writer.Write(Data.Length);
                
                _init = false;

                return false;
            }

            writer.Write((int)OperationCode.UserFrameTransfer);
            writer.Write((int)OperationStatus.Push);

            // just for check?
            writer.Write(_offset);

            // todo configurable chunks size support
            // account for 4 bytes taken by chunkSize info!!
            var dataChunkSize = Math.Min(Data.Length - _offset, writer.RemainingPacketWriteCapacity - 4);
            writer.Write(dataChunkSize);

            int writeOffset;
            writer.ReserveForWrite(dataChunkSize, out writeOffset);
            unsafe
            {
                var data = Data;
                var buffer = writer.GetBuffer();
                fixed (byte* bufferPtr = buffer)
                fixed (byte* dataSourcePtr = data)
                {
                    DataManipulation.Copy(dataSourcePtr + _offset, bufferPtr + writeOffset, (uint)dataChunkSize);
                }                
            }
            _offset += dataChunkSize;
            return _offset == Data.Length;
        }

        public override bool ReadData(ReadBuffer reader)
        {
            if (Data == null)
            {
                // header 
                var status = (OperationStatus)reader.ReadInt32();
                var dataSize = reader.ReadInt32();

                Data = new byte[dataSize];
                return false;
            }
            
            var operationStatus = (OperationStatus)reader.ReadInt32();

            // check?
            var offset = reader.ReadInt32();
            var dataLength = reader.ReadInt32();

            int readOffset;
            reader.ReserveForReading(dataLength, out readOffset);
                        
            unsafe
            {   
                var data = Data;
                var readBuffer = reader.GetBuffer();
                fixed (byte* readBufferPtr = readBuffer)
                fixed (byte* targetDataPtr = data)
                {
                    DataManipulation.Copy(readBufferPtr + readOffset, targetDataPtr + _offset, (uint)dataLength);
                    _offset += dataLength;
                }
            }
            
            return _offset == Data.Length;
        }
    }
}
