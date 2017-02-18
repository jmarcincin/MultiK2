using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    enum ReaderType { Audio, Color, Depth, Body, BodyIndex, Infrared, Infrared2 }

    class OpenReader
    {
        public ReaderType Type { get; }
                
        public ReaderConfig Config { get; }

        public OpenReader(ReaderType readerType, ReaderConfig config)
        {
            Type = readerType;
            Config = config;
        }
        
        public void WriteRequest(DataWriter writer)
        {
            writer.WriteInt32((int)OperationCode.OpenReader);
            writer.WriteInt32((int)OperationType.Request);
            writer.WriteInt32((int)Type);
            writer.WriteInt32((int)Config);
        }

        public void WriteResponse(DataWriter writer, OperationType response)
        {
            writer.WriteInt32((int)OperationCode.OpenReader);
            writer.WriteInt32((int)response);
            writer.WriteInt32((int)Type);
            writer.WriteInt32((int)Config);
        }

        public static OpenReader ReadData(DataReader reader)
        {
            var type = reader.ReadInt32();
            var config = reader.ReadInt32();
            
            return new OpenReader((ReaderType)type, (ReaderConfig)config);
        }
    }
}
