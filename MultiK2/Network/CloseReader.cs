using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{    
    internal class CloseReader : INetworkCommandAsync
    {
        private ManualResetEvent _signal;
        private INetworkCommandAsync _response;

        public const uint Size = 8;

        public ReaderType Type { get; private set; }
         
        public OperationStatus Status { get; private set; }

        public CloseReader(ReaderType readerType)
        {
            Type = readerType;
            _signal = new ManualResetEvent(false);
        }

        public CloseReader()
        {
        }

        public void WriteRequest(DataWriter writer)
        {
            writer.WriteInt32((int)OperationCode.CloseReader);
            writer.WriteInt32((int)OperationStatus.Request);
            writer.WriteInt32((int)Type);
        }

        public void WriteResponse(DataWriter writer, OperationStatus response)
        {
            writer.WriteInt32((int)OperationCode.CloseReader);
            writer.WriteInt32((int)response);
            writer.WriteInt32((int)Type);
        }

        public async Task ReadRequest(DataReader reader)
        {
            await reader.LoadAsync(Size);

            // todo: check operation type
            Status = (OperationStatus)reader.ReadInt32();
            Type = (ReaderType)reader.ReadInt32();
        }

        public async Task ReadResponse(DataReader reader)
        {
            await reader.LoadAsync(Size);

            // todo: check operation type
            Status = (OperationStatus)reader.ReadInt32();
            Type = (ReaderType)reader.ReadInt32();
        }

        public bool MatchingResponse(INetworkCommandAsync commandResponse)
        {
            var command = commandResponse as CloseReader;
            return command != null && command.Type == Type;
        }

        public Task<INetworkCommandAsync> AwaitResponseAsync()
        {
            return Task.Run(
                () =>
                {
                    _signal.WaitOne();
                    return _response;
                });
        }

        public void SetResponse(INetworkCommandAsync response)
        {
            _response = response;
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _signal?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
