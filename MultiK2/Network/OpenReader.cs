using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal enum ReaderType { Audio, Color, Depth, Body, BodyIndex, Infrared, Infrared2, UserDefined }

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
            Status = OperationStatus.Request;
            _signal = new ManualResetEvent(false);
        }

        public OpenReader() { }

        public void WriteCommand(WriteBuffer writer)
        {
            writer.Write((int)OperationCode.OpenReader);
            writer.Write((int)Status);
            writer.Write((int)Type);
            writer.Write((int)Config);
        }
        
        public void ReadCommand(ReadBuffer reader)
        {
            Status = (OperationStatus)reader.ReadInt32();
            Type = (ReaderType)reader.ReadInt32();
            Config = (ReaderConfig)reader.ReadInt32();
        }

        public void SetResponse(OperationStatus response)
        {
            Status = response;
        }

        public bool MatchingResponse(INetworkCommandAsync commandResponse)
        {
            var command = commandResponse as OpenReader;
            return command != null && command.Type == Type && command.Config == Config;
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
