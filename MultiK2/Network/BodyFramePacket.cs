using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using MultiK2.Tracking;

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

        public override bool WriteData(DataWriter writer)
        {
            // frame packet generic header
            writer.WriteInt32((int)OperationCode.BodyFrameTransfer);
            writer.WriteInt32((int)OperationStatus.PushInit);
                        
            writer.WriteInt64(BodyFrame.SystemRelativeTime.Value.Ticks);
            writer.WriteUInt32((uint)BodyFrame.BinaryData.Length);
            writer.WriteBytes(BodyFrame.BinaryData);

            return true;            
        }

        public override async Task<bool> ReadDataAsync(DataReader reader)
        {
            await reader.LoadAsync(16);

            // TODO; validate status
            var operationStatus = (OperationStatus)reader.ReadInt32();
            var systemTime = reader.ReadInt64();
            var dataLenght = reader.ReadUInt32();

            await reader.LoadAsync(dataLenght);
            var bodyData = new byte[dataLenght];
            reader.ReadBytes(bodyData);

            BodyFrame = BodyFrame.Parse(bodyData, TimeSpan.FromTicks(systemTime));

            return true;
        }
    }
}
