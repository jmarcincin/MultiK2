using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{    
    class CloseReader
    {   
        public ReaderType Type { get; }
                
        public CloseReader(ReaderType readerType)
        {
            Type = readerType;
        }
        
        public void WriteRequest(DataWriter writer)
        {
            writer.WriteInt32((int)OperationCode.CloseReader);
            writer.WriteInt32((int)OperationType.Request);
            writer.WriteInt32((int)Type);
        }

        public void WriteResponse(DataWriter writer, OperationType response)
        {
            writer.WriteInt32((int)OperationCode.CloseReader);
            writer.WriteInt32((int)response);
            writer.WriteInt32((int)Type);
        }

        public static CloseReader ReadData(DataReader reader)
        {
            var type = reader.ReadInt32();

            return new CloseReader((ReaderType)type);
        }
    }
}
