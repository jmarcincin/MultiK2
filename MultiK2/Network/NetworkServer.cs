using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    enum TransferState
    {
        Idle,
        Busy
    }

    internal class NetworkServer :NetworkBase
    {
        private Sensor _sensor;
        private StreamSocketListener _socketListener;
        
        public NetworkServer(Sensor kinectSensor)
        {
            _sensor = kinectSensor;
        }

        public async Task StartListener()
        {
            if (_socketListener == null) { }

            _socketListener = _socketListener ?? new StreamSocketListener();
            _socketListener.ConnectionReceived += SocketListener_ConnectionReceived;
            await _socketListener.BindEndpointAsync(null, "8599");
        }
        
        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (_sensorConnection != null)
            {
                // send sensor busy?
                args.Socket.Dispose();
            }

            _sensorConnection = args.Socket;
            _dataReader = new DataReader(_sensorConnection.InputStream);
            _dataWriter = new DataWriter(_sensorConnection.OutputStream);

            _dataWriter.ByteOrder = ByteOrder.LittleEndian;
            _dataReader.ByteOrder = ByteOrder.LittleEndian;

            var operation = await _dataReader.LoadAsync(HelloPacket.Size);

            var hello = HelloPacket.Read(_dataReader);

            // reply with version - resend hello packet
            hello.Write(_dataWriter);
            await _dataWriter.StoreAsync();
            await _dataWriter.FlushAsync();

            // TODO receive loop cancellation
            Task.Run(ServerReceiveLoop);
        }

        private async Task ServerReceiveLoop()
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
                    case OperationCode.OpenReader:
                        var openRequest = new OpenReader();
                        await openRequest.ReadRequest(_dataReader);
                        
                        // handle open request + start receiving & resending frames
                        if (openRequest.Type == ReaderType.Body)
                        {
                            var bodyReader = await _sensor.OpenBodyFrameReaderAsync();
                            if (bodyReader != null)
                            {
                                bodyReader.FrameArrived -= BodyReader_FrameArrived;
                                bodyReader.FrameArrived += BodyReader_FrameArrived;

                                // subscribe + positive response
                                openRequest.WriteResponse(_dataWriter, OperationStatus.ResponseSuccess);
                            }
                            else
                            {
                                openRequest.WriteResponse(_dataWriter, OperationStatus.ResponseFail);
                            }
                        }
                        else
                        {
                            openRequest.WriteResponse(_dataWriter, OperationStatus.ResponseFailNotAvailable);
                        }
                        break;
                    case OperationCode.CloseReader:
                        var closeRequest = new CloseReader();
                        await closeRequest.ReadRequest(_dataReader);
                        // handle close request

                        closeRequest.WriteResponse(_dataWriter, OperationStatus.ResponseSuccess);

                        break;
                    case OperationCode.CloseSensor:
                        // todo: close all opened readers but keep kinect open / listen for connections
                        _sensorConnection.Dispose();
                        _sensorConnection = null;

                        // break task loop
                        return;
                }

                await _dataWriter.StoreAsync();
                await _dataWriter.FlushAsync();
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            // stream over wire
            var bodyPacket = new BodyFramePacket(e.BodyFrame);
            
            // enqueue
            SendData(bodyPacket);
        }
    }
}
