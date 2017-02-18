using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    abstract class FramePacket
    {
        public ReaderType Type { get; }

        protected FramePacket(ReaderType type)
        {
            Type = type;
        }

        public abstract Task<bool> WriteData(DataWriter writer);
    }
}
