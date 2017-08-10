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
            Status = OperationStatus.Request;
            _signal = new ManualResetEvent(false);
        }

        public CloseReader()
        {
        }

        public void SetResponse(OperationStatus response)
        {
            // todo:check value
            Status = response;
        }

        public void WriteCommand(WriteBuffer writer)
        {
            writer.Write((int)OperationCode.CloseReader);
            writer.Write((int)Status);
            writer.Write((int)Type);
        }

        public void ReadCommand(ReadBuffer reader)
        {           
            // todo: check operation type
            Status = (OperationStatus)reader.ReadInt32();
            Type = (ReaderType)reader.ReadInt32();
        }
        
        public bool MatchingResponse(INetworkCommandAsync commandResponse)
        {
            var command = commandResponse as CloseReader;
            return command != null && command.Type == Type;
        }

        Task<INetworkCommandAsync> INetworkCommandAsync.AwaitResponseAsync()
        {
            return Task.Run(
                () =>
                {
                    _signal.WaitOne();
                    return _response;
                });
        }

        void INetworkCommandAsync.SetResponse(INetworkCommandAsync response)
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
