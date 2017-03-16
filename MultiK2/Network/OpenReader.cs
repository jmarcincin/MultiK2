using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal enum ReaderType { Audio, Color, Depth, Body, BodyIndex, Infrared, Infrared2 }

    internal class OpenReader : INetworkCommandAsync
    {
        private ManualResetEvent _signal;
        private INetworkCommandAsync _response;

        public const uint Size = 12;

        public ReaderType Type { get; private set; }
                
        public ReaderConfig Config { get; private set; }

        public OperationStatus Status { get; private set; }

        public OpenReader(ReaderType readerType, ReaderConfig config)
        {
            Type = readerType;
            Config = config;
            _signal = new ManualResetEvent(false);
        }

        public OpenReader() { }

        public void WriteRequest(DataWriter writer)
        {
            writer.WriteInt32((int)OperationCode.OpenReader);
            writer.WriteInt32((int)OperationStatus.Request);
            writer.WriteInt32((int)Type);
            writer.WriteInt32((int)Config);
        }

        public void WriteResponse(DataWriter writer, OperationStatus response)
        {
            writer.WriteInt32((int)OperationCode.OpenReader);
            writer.WriteInt32((int)response);
            writer.WriteInt32((int)Type);
            writer.WriteInt32((int)Config);
        }

        public async Task ReadRequest(DataReader reader)
        {
            await reader.LoadAsync(Size);

            Status = (OperationStatus)reader.ReadInt32();
            Type = (ReaderType)reader.ReadInt32();
            Config = (ReaderConfig)reader.ReadInt32();
        }

        public async Task ReadResponse(DataReader reader)
        {
            await ReadRequest(reader);
        }

        public bool MatchingResponse(INetworkCommandAsync commandResponse)
        {
            var command = commandResponse as OpenReader;
            return command != null && command.Type == Type && command.Config == Config;
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
            _signal.Set();
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
