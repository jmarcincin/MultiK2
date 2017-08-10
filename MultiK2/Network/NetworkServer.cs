using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

    internal class NetworkServer : NetworkBase
    {
        private Sensor _sensor;
        //private StreamSocketListener _socketListener;
        private CancellationTokenSource _cancellationTokenSource;
        private Socket _socketListener;
        
        public NetworkServer(Sensor kinectSensor)
        {
            _sensor = kinectSensor;
        }

        public async Task StartListener()
        {
            if (_socketListener == null)
            {
                _socketListener = new Socket(SocketType.Stream, ProtocolType.Tcp);
                _socketListener.Bind(new IPEndPoint(IPAddress.Any, _sensor.ServerPort));
                _socketListener.Listen(2);

                ResetAccept(null);
            }

                /*
            if (_socketListener == null) { }

            _socketListener = _socketListener ?? new StreamSocketListener();
            _socketListener.ConnectionReceived += SocketListener_ConnectionReceived;
            await _socketListener.BindEndpointAsync(null, "8599");*/
        }

        private void ResetAccept(SocketAsyncEventArgs acceptArgs)
        {
            if (acceptArgs == null)
            {
                acceptArgs = new SocketAsyncEventArgs();
                acceptArgs.Completed += SocketOperationCompleted;
            }
            else
            {
                acceptArgs.AcceptSocket = null;
            }

            var async = _socketListener.AcceptAsync(acceptArgs);
            if (!async)
            {
                ConnectionReceived(acceptArgs);
            }
        }

        private async void ConnectionReceived(SocketAsyncEventArgs acceptArgs)
        {
            if (_connection != null)
            {
                // send sensor busy?
                acceptArgs.AcceptSocket.Dispose();
            }
            
            _connection = acceptArgs.AcceptSocket;

            var helloData = await _connection.ReceiveAsync(HelloPacket.Size);

            // todo check version header 
            var helloPacket = HelloPacket.Parse(helloData, 0);

            var helloResponse = new HelloPacket(1);
            await _connection.SendAsync(helloResponse.GetData());
            
            StartReceivingData();
            StartSendingData();
        }

        private void SocketOperationCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    {
                        // process accept
                        ConnectionReceived(e);
                        break;
                    }
            }
        }
        
        private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            var depthPacket = new DepthFramePacket(e.Bitmap, e.CameraIntrinsics, _sensor.GetCoordinateMapper().DepthToColor);
            SendFrameData(depthPacket);
        }

        private void BodyIndexReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
        {
            var bodyIndexPacket = new BodyIndexFramePacket(e.Bitmap, e.CameraIntrinsics, _sensor.GetCoordinateMapper().DepthToColor);
            SendFrameData(bodyIndexPacket);
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            // stream over wire
            var bodyPacket = new BodyFramePacket(e.BodyFrame);
            
            // enqueue
            SendFrameData(bodyPacket);
        }
        
        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            var colorPacket = new ColorFramePacket(e.Bitmap, e.CameraIntrinsics, _sensor.GetCoordinateMapper().ColorToDepth);
            SendFrameData(colorPacket);
        }

        protected async override void ProcessReceivedData(ReadBuffer receiveBuffer)
        {
            var opCode = (OperationCode)receiveBuffer.ReadInt32();

            // TODO: validate operation type when processing
            //var operationType = (OperationStatus)_dataReader.ReadInt32();

            switch (opCode)
            {
                case OperationCode.OpenReader:
                    var openRequest = new OpenReader();
                    openRequest.ReadCommand(receiveBuffer);

                    // handle open request + start receiving & resending frames
                    if (openRequest.Type == ReaderType.Body)
                    {
                        var bodyReader = _sensor.OpenBodyFrameReaderAsync().AsTask().Result;
                        if (bodyReader != null)
                        {
                            bodyReader.FrameArrived -= BodyReader_FrameArrived;
                            bodyReader.FrameArrived += BodyReader_FrameArrived;

                            // subscribe + positive response
                            openRequest.SetResponse(OperationStatus.ResponseSuccess);                            
                        }
                        else
                        {
                            openRequest.SetResponse(OperationStatus.ResponseFail);                            
                        }
                    }
                    else if (openRequest.Type == ReaderType.Depth)
                    {
                        var depthReader = _sensor.OpenDepthFrameReaderAsync().AsTask().Result;
                        if (depthReader != null)
                        {
                            depthReader.FrameArrived -= DepthReader_FrameArrived;
                            depthReader.FrameArrived += DepthReader_FrameArrived;

                            openRequest.SetResponse(OperationStatus.ResponseSuccess);
                        }
                        else
                        {
                            openRequest.SetResponse(OperationStatus.ResponseFail);
                        }
                    }
                    else if (openRequest.Type == ReaderType.BodyIndex)
                    {
                        var bodyIndexReader = _sensor.OpenBodyIndexFrameReaderAsync().AsTask().Result;
                        if (bodyIndexReader != null)
                        {
                            bodyIndexReader.FrameArrived -= BodyIndexReader_FrameArrived;
                            bodyIndexReader.FrameArrived += BodyIndexReader_FrameArrived;

                            openRequest.SetResponse(OperationStatus.ResponseSuccess);
                        }
                        else
                        {
                            openRequest.SetResponse(OperationStatus.ResponseFail);
                        }
                    }
                    else if (openRequest.Type == ReaderType.Color)
                    {
                        var colorReader = _sensor.OpenColorFrameReaderAsync().AsTask().Result;
                        if (colorReader != null)
                        {
                            colorReader.FrameArrived -= ColorReader_FrameArrived;
                            colorReader.FrameArrived += ColorReader_FrameArrived;
                            openRequest.SetResponse(OperationStatus.ResponseSuccess);
                        }
                        else
                        {
                            openRequest.SetResponse(OperationStatus.ResponseFail);
                        }
                    }
                    else
                    {
                        openRequest.SetResponse(OperationStatus.ResponseFailNotAvailable);
                    }

                    // send response should happen before subscription (possible race condition when frame gets send before reply to request gets transmitted)
                    SendRequestCommand(openRequest);
                    break;
                case OperationCode.CloseReader:
                    var closeRequest = new CloseReader();
                    closeRequest.ReadCommand(receiveBuffer);
                    
                    // TODO: handle close request
                    closeRequest.SetResponse(OperationStatus.ResponseSuccess);
                    SendRequestCommand(closeRequest);
                    break;
                case OperationCode.CloseSensor:
                    // todo: close all opened readers but keep kinect open / listen for connections
                    _connection.Dispose();
                    _connection = null;

                    // break task loop
                    return;
            }
        }
    }
}
