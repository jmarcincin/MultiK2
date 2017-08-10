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
        public const int Size = 8;

        // "MK2P"
        private static byte[] ProtocolHeader = { 0x4D, 0x4B, 0x32, 0x50, 0, 0, 0, 0 };
        
        public uint Version { get; }

        public HelloPacket(byte version)
        {
            Version = version;
        }
        
        public byte[] GetData()
        {
            var copy = ProtocolHeader.ToArray();
            copy[4] = (byte)Version;

            return copy;
        }

        public static HelloPacket Parse(byte[] data, int offset)
        {
            if (data.Length < offset + 8)
            {
                return null;
            }

            for (var i = 0; i < 4; i++)
            {
                if (data[offset + i] != ProtocolHeader[i])
                {
                    return null;
                } 
            }
            
            return new HelloPacket(data[offset + 4]);
        }
    }
}
