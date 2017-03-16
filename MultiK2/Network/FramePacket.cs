using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal abstract class FramePacket
    {
        public ReaderType FrameType { get; }

        protected FramePacket(ReaderType type)
        {
            FrameType = type;
        }

        public abstract bool WriteData(DataWriter writer);

        public abstract Task<bool> ReadDataAsync(DataReader reader);
    }
}
