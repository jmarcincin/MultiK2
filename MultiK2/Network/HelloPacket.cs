using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal class HelloPacket
    {
        public const uint Size = 8;

        // "MK2P"
        private const uint ProtocolHeader = 0x4D | 0x4B << 8 | 0x32 << 16 | 0x50 << 24;
        
        public uint Version { get; }

        public HelloPacket(uint version)
        {
            Version = version;
        }
        
        public void Write(DataWriter writer)
        {
            writer.WriteUInt32(ProtocolHeader);
            writer.WriteUInt32(Version);
        }

        public static HelloPacket Read(DataReader reader)
        {
            var protocolHeader = reader.ReadUInt32();

            if (protocolHeader != ProtocolHeader)
            {
                throw new Exception("Header does not match");
            }

            var version = reader.ReadUInt32();
            return new HelloPacket(version);
        }
    }
}
