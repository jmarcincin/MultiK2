using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal interface INetworkCommandAsync : IDisposable
    {
        OperationStatus Status { get; }
                
        void SetResponse(OperationStatus response);

        void WriteCommand(WriteBuffer writer);

        void ReadCommand(ReadBuffer reader);
        
        bool MatchingResponse(INetworkCommandAsync commandResponse);

        Task<INetworkCommandAsync> AwaitResponseAsync();

        void SetResponse(INetworkCommandAsync response);
    }
}
