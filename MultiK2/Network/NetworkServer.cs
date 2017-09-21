using MultiK2.Utils;
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
    internal class NetworkServer : NetworkBase
    {
        private readonly Sensor _sensor;
        private CancellationTokenSource _cancellationTokenSource;
        private Socket _socketListener;
        
        public NetworkServer(Sensor kinectSensor)
        {
            _sensor = kinectSensor;
        }

        public void StartListener()
        {
            _socketListener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socketListener.Bind(new IPEndPoint(IPAddress.Any, _sensor.ServerPort));
            _socketListener.Listen(1);

            var acceptArgs = new SocketAsyncEventArgs();
            acceptArgs.Completed += SocketOperationCompleted;
            Task.Run(() => ResetAccept(acceptArgs));
        }

        private void ResetAccept(SocketAsyncEventArgs acceptArgs)
        {
            acceptArgs.AcceptSocket = null;
            
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
                acceptArgs.AcceptSocket?.Dispose();
            }
            else
            {
                try
                {
                    _connection = acceptArgs.AcceptSocket;
                    var helloData = await _connection.ReceiveAsync(HelloPacket.Size);

                    // todo check version header 
                    var helloPacket = HelloPacket.Parse(helloData, 0);

                    var helloResponse = new HelloPacket(1);
                    await _connection.SendAsync(helloResponse.GetData());

                    Init((IPEndPoint)_connection.RemoteEndPoint);
                    StartReceivingData();
                    StartSendingData();
                }
                catch (Exception)
                {
                    // todo log?
                    _connection = null;
                }
            }
            ResetAccept(acceptArgs);
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
            var bodyPacket = new BodyFramePacket(e.BodyFrame);
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

            // MAKE SURE TO read all data from receiveBuffer before awaiting async ooperation!
            switch (opCode)
            {
                case OperationCode.OpenReader:
                    {
                        var openRequest = new OpenReader();
                        openRequest.ReadCommand(receiveBuffer);

                        // handle open request + start receiving & resending frames
                        switch (openRequest.Type)
                        {
                            case ReaderType.Body:
                                {
                                    var bodyReader = await _sensor.OpenBodyFrameReaderAsync();
                                    if (bodyReader != null)
                                    {
                                        bodyReader.FrameArrived -= BodyReader_FrameArrived;
                                        bodyReader.FrameArrived += BodyReader_FrameArrived;
                                        openRequest.SetResponse(OperationStatus.ResponseSuccess);
                                    }
                                    else
                                    {
                                        openRequest.SetResponse(OperationStatus.ResponseFail);
                                    }
                                    break;
                                }
                            case ReaderType.BodyIndex:
                                {
                                    var bodyIndexReader = await _sensor.OpenBodyIndexFrameReaderAsync();
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
                                    break;
                                }
                            case ReaderType.Color:
                                {
                                    var colorReader = await _sensor.OpenColorFrameReaderAsync(openRequest.Config);
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
                                    break;
                                }
                            case ReaderType.Depth:
                                {
                                    var depthReader = await _sensor.OpenDepthFrameReaderAsync();
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
                                    break;
                                }
                            default:
                                {
                                    openRequest.SetResponse(OperationStatus.ResponseFailNotAvailable);
                                    break;
                                }
                        }

                        // send response should happen before subscription (possible race condition when frame gets send before reply to request gets transmitted)
                        SendRequestCommand(openRequest);
                        break;
                    }
                case OperationCode.CloseReader:
                    {
                        var closeRequest = new CloseReader();
                        closeRequest.ReadCommand(receiveBuffer);

                        switch (closeRequest.Type)
                        {
                            case ReaderType.Body:
                                await _sensor.BodyReader?.CloseAsync();
                                break;
                            case ReaderType.BodyIndex:
                                await _sensor.BodyIndexReader?.CloseAsync();
                                break;
                            case ReaderType.Color:
                                await _sensor.ColorReader?.CloseAsync();
                                break;
                            case ReaderType.Depth:
                                await _sensor.DepthReader?.CloseAsync();
                                break;
                            default:
                                closeRequest.SetResponse(OperationStatus.ResponseFailNotAvailable);
                                SendRequestCommand(closeRequest);
                                return;
                        }

                        closeRequest.SetResponse(OperationStatus.ResponseSuccess);
                        SendRequestCommand(closeRequest);
                        break;
                    }
                case OperationCode.CloseSensor:
                    {
                        // close all opened readers but keep kinect open / listen for connections
                        _connection.Dispose();
                        _connection = null;
                        
                        await _sensor.BodyReader?.CloseAsync();
                        await _sensor.BodyIndexReader?.CloseAsync();
                        await _sensor.ColorReader?.CloseAsync();
                        await _sensor.DepthReader?.CloseAsync();
                        
                        return;
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
