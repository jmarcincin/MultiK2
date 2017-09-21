using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal class NetworkClient : NetworkBase
    {
        private IPEndPoint _sensorAddress;

        public event EventHandler<BodyFramePacket> BodyFrameArrived;

        public event EventHandler<DepthFramePacket> DepthFrameArrived;

        public event EventHandler<BodyIndexFramePacket> BodyIndexFrameArrived;

        public event EventHandler<ColorFramePacket> ColorFrameArrived;

        public bool IsConnected { get; private set; }

        public NetworkClient(IPEndPoint sensorAddress)
        {
            _sensorAddress = sensorAddress;
        }

        public async Task<bool> OpenNetworkAsync()
        {
            if (_connection != null)
            {
                return true;
            }

            // TODO winsock/Socket vs. RT sockets
            try
            {
                var con = new Socket(SocketType.Stream, ProtocolType.Tcp);

                // write header/ protocol version/ connection config
                var helloPacket = new HelloPacket(1);
                _connection = await con.ConnectAsync(_sensorAddress, helloPacket.GetData());
                
                // read the response from the server

                var helloResponse = await _connection.ReceiveAsync(HelloPacket.Size);                
                helloPacket = HelloPacket.Parse(helloResponse, 0);

                // TODO: compare protocol versions

                // Start receive loop - TODO: cancellation token
                Init((IPEndPoint)_connection.RemoteEndPoint);
                StartReceivingData();
                StartSendingData();
            }
            catch (Exception)
            {
                _connection?.Dispose();
                _connection = null;
                
                IsConnected = false;
                return false;
            }

            IsConnected = true;
            return true;
        }
        
        public Task<INetworkCommandAsync> SendCommandAsync(INetworkCommandAsync command)
        {
            SendRequestCommand(command);
            return command.AwaitResponseAsync();
        } 
        
        protected override void ProcessReceivedData(ReadBuffer receiveBuffer)
        {
            var opCode = (OperationCode)receiveBuffer.ReadInt32();

            // TODO: validate operation type when processing
            //var operationType = (OperationStatus)_dataReader.ReadInt32();

            switch (opCode)
            {
                // process responses
                case OperationCode.OpenReader:
                    {
                        // find first unprocessed request
                        var openResponse = new OpenReader();
                        openResponse.ReadCommand(receiveBuffer);
                        HandleCommandResponse(openResponse);
                        break;
                    }
                case OperationCode.CloseReader:
                    {
                        var closeResponse = new CloseReader();
                        closeResponse.ReadCommand(receiveBuffer);
                        HandleCommandResponse(closeResponse);
                        break;
                    }
                case OperationCode.BodyFrameTransfer:
                    {
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.Body, out activeReceive))
                        {
                            // todo: verify that operation status is not push init
                        }
                        else
                        {
                            activeReceive = new BodyFramePacket();
                            _activeFrameReceives[ReaderType.Body] = activeReceive;
                        }

                        var finishedReading = activeReceive.ReadData(receiveBuffer);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.Body);
                            var subs = BodyFrameArrived;
                            if (subs != null)
                            {
                                Task.Run(() => subs(this, (BodyFramePacket)activeReceive));
                            }
                        }
                        break;
                    }
                case OperationCode.DepthFrameTransfer:
                    {
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.Depth, out activeReceive))
                        {
                            // todo: verify that operation status is not push init
                        }
                        else
                        {
                            activeReceive = new DepthFramePacket();
                            _activeFrameReceives[ReaderType.Depth] = activeReceive;
                        }

                        var finishedReading = activeReceive.ReadData(receiveBuffer);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.Depth);
                            var subs = DepthFrameArrived;
                            if (subs != null)
                            {
                                Task.Run(() => subs(this, (DepthFramePacket)activeReceive));
                            }
                        }
                        break;
                    }
                case OperationCode.BodyIndexFrameTransfer:
                    {
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.BodyIndex, out activeReceive))
                        {
                            // todo: verify that operation status is not push init
                        }
                        else
                        {
                            activeReceive = new BodyIndexFramePacket();
                            _activeFrameReceives[ReaderType.BodyIndex] = activeReceive;
                        }

                        var finishedReading = activeReceive.ReadData(receiveBuffer);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.BodyIndex);
                            var subs = BodyIndexFrameArrived;
                            if (subs != null)
                            {
                                Task.Run(() => subs(this, (BodyIndexFramePacket)activeReceive));
                            }
                        }
                        break;
                    }
                case OperationCode.ColorFrameTransfer:
                    {
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.Color, out activeReceive))
                        {
                            // todo: verify that operation status is not push init
                        }
                        else
                        {
                            activeReceive = new ColorFramePacket();
                            _activeFrameReceives[ReaderType.Color] = activeReceive;
                        }

                        var finishedReading = activeReceive.ReadData(receiveBuffer);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.Color);
                            var subs = ColorFrameArrived;
                            if (subs != null)
                            {
                                Task.Run(() => subs(this, (ColorFramePacket)activeReceive));
                            }
                        }
                        break;
                    }
                case OperationCode.UserFrameTransfer:
                    {
                        FramePacket activeReceive;
                        if (_activeFrameReceives.TryGetValue(ReaderType.UserDefined, out activeReceive))
                        {
                            // todo: verify that operation status is not push init
                        }
                        else
                        {
                            activeReceive = new CustomFramePacket();
                            _activeFrameReceives[ReaderType.UserDefined] = activeReceive;
                        }

                        var finishedReading = activeReceive.ReadData(receiveBuffer);
                        if (finishedReading)
                        {
                            _activeFrameReceives.Remove(ReaderType.UserDefined);
                            OnCustomDataReceived((CustomFramePacket)activeReceive);
                        }
                        break;
                    }
            }
        }
    }
}
