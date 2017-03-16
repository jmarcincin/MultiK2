using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal class NetworkClient : NetworkBase
    {
        private EndpointPair _sensorAddress;

        public event EventHandler<BodyFramePacket> BodyFrameArrived;
        
        public bool IsConnected { get; private set; }

        public NetworkClient(EndpointPair sensorAddress)
        {
            _sensorAddress = sensorAddress;
        }

        public async Task<bool> OpenNetworkAsync()
        {
            if (_sensorConnection != null)
            {
                return true;
            }

            // TODO winsock/Socket vs. RT sockets
            try
            {
                _sensorConnection = new StreamSocket();
                await _sensorConnection.ConnectAsync(_sensorAddress);

                _dataReader = new DataReader(_sensorConnection.InputStream);
                _dataWriter = new DataWriter(_sensorConnection.OutputStream);

                _dataWriter.ByteOrder = ByteOrder.LittleEndian;
                _dataReader.ByteOrder = ByteOrder.LittleEndian;

                // write header/ protocol version/ connection config
                var helloPacket = new HelloPacket(1);
                helloPacket.Write(_dataWriter);
                await _dataWriter.StoreAsync();
                await _dataWriter.FlushAsync();

                // read the response from the server
                var asyncOperation = _dataReader.LoadAsync(HelloPacket.Size);

                // TODO: timeout handling
                if (!asyncOperation.AsTask().Wait(50000))
                {
                    asyncOperation.Close();
                    asyncOperation.Cancel();
                }
                helloPacket = HelloPacket.Read(_dataReader);

                // TODO: compare protocol versions

                // Start receive loop - TODO: cancellation token
                Task.Run(ClientReceiveLoop);
            }
            catch (Exception)
            {
                _sensorConnection?.Dispose();
                _sensorConnection = null;
                _dataReader?.Dispose();
                _dataReader = null;
                _dataWriter?.Dispose();
                _dataWriter = null;

                IsConnected = false;
                return false;
            }

            IsConnected = true;
            return true;
        }

        public void CloseConnection()
        {
            _sensorConnection?.Dispose();
            _sensorConnection = null;
            _dataReader?.Dispose();
            _dataReader = null;
            _dataWriter?.Dispose();
            _dataWriter = null;
        }

        public Task<INetworkCommandAsync> SendCommandAsync(INetworkCommandAsync command)
        {
            SendRequestCommand(command);
            return command.AwaitResponseAsync();
        } 

        private async Task ClientReceiveLoop()
        {
            while (true)
            {
                // load packet op code 
                await _dataReader.LoadAsync(4);
                var opCode = (OperationCode)_dataReader.ReadInt32();

                // TODO: validate operation type when processing
                //var operationType = (OperationStatus)_dataReader.ReadInt32();

                switch (opCode)
                {
                    // process responses
                    case OperationCode.OpenReader:
                        {
                            // find first unprocessed request
                            var openResponse = new OpenReader();
                            await openResponse.ReadResponse(_dataReader);
                            HandleResponse(openResponse);
                            break;
                        }
                    case OperationCode.CloseReader:
                        {
                            var closeResponse = new CloseReader();
                            await closeResponse.ReadResponse(_dataReader);
                            HandleResponse(closeResponse);
                            break;
                        }
                    case OperationCode.BodyFrameTransfer:
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.Body, out activeReceive))
                        {                            
                        }
                        else
                        {
                            activeReceive = new BodyFramePacket();
                            _activeFrameReceives[ReaderType.Body] = activeReceive;
                        }

                        var finishedReading = await activeReceive.ReadDataAsync(_dataReader);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.Body);
                            BodyFrameArrived.BeginInvoke(this, (BodyFramePacket)activeReceive, null, null);
                        }

                        break;
                }
            }
        }
    }
}
