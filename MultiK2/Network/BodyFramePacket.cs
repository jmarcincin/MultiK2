using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using MultiK2.Tracking;
using MultiK2.Utils;

namespace MultiK2.Network
{
    internal class BodyFramePacket : FramePacket
    {
        public BodyFrame BodyFrame { get; private set; }
        
        public BodyFramePacket(BodyFrame frame) : this()
        {
            BodyFrame = frame;
        }

        public BodyFramePacket() : base(ReaderType.Body)
        {
        }

        public override bool WriteData(WriteBuffer writer)
        {
            // frame packet generic header
            writer.Write((int)OperationCode.BodyFrameTransfer);
            writer.Write((int)OperationStatus.PushInit);
                        
            writer.Write(BodyFrame.SystemRelativeTime.Value.Ticks);
            writer.Write(BodyFrame.BinaryData.Length);

            int writeOffset;
            writer.ReserveForWrite(BodyFrame.BinaryData.Length, out writeOffset);
            unsafe
            {
                var buffer = writer.GetBuffer();
                fixed (byte* bufferPtr = buffer)
                fixed (byte* sourceDataPtr = BodyFrame.BinaryData)
                {
                    DataManipulation.Copy(sourceDataPtr, bufferPtr + writeOffset, (uint)BodyFrame.BinaryData.Length);
                }
            }
            
            return true;            
        }

        public override bool ReadData(ReadBuffer reader)
        {            
            // TODO; validate status
            var operationStatus = (OperationStatus)reader.ReadInt32();
            var systemTime = reader.ReadInt64();
            var dataLenght = reader.ReadInt32();
            var bodyData = new byte[dataLenght];

            int readOffset;
            reader.ReserveForReading(dataLenght, out readOffset);

            unsafe
            {
                var buffer = reader.GetBuffer();
                fixed (byte* bodyDataPtr = bodyData)
                fixed (byte* bufferPtr = buffer)
                {
                    DataManipulation.Copy(bufferPtr + readOffset, bodyDataPtr, (uint)dataLenght);
                }
            }
            BodyFrame = BodyFrame.Parse(bodyData, TimeSpan.FromTicks(systemTime));

            return true;
        }
    }
}
