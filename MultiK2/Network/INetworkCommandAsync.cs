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

        void WriteRequest(DataWriter writer);

        void WriteResponse(DataWriter writer, OperationStatus response);

        Task ReadRequest(DataReader reader);

        Task ReadResponse(DataReader reader);

        bool MatchingResponse(INetworkCommandAsync commandResponse);

        Task<INetworkCommandAsync> AwaitResponseAsync();

        void SetResponse(INetworkCommandAsync response);
    }
}
