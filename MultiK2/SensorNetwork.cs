using MultiK2.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Capture.Frames;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace MultiK2
{
    public sealed partial class Sensor
    {   
        public static Sensor CreateNetworkSensor(EndpointPair remoteEndPoint)
        {
            return new Sensor(remoteEndPoint);
        }

        private EndpointPair _sensorAddress;
        private StreamSocket _sensorConnection;
        private DataReader _dataReader;
        private DataWriter _dataWriter;

        private StreamSocketListener _socketListener;

        private Sensor(EndpointPair remoteEndPoint)
        {
            _sensorAddress = remoteEndPoint;
            Type = SensorType.NetworkClient;
        }

        private async Task StartListener()
        {
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

            var operation = await _dataReader.LoadAsync(8);

            var hello = HelloPacket.Read(_dataReader);

            // reply with version - resend hello packet
            hello.Write(_dataWriter);
            await _dataWriter.StoreAsync();
            await _dataWriter.FlushAsync();              

            // todo: start receive loop
        }

        private async Task<bool> OpenNetworkAsync()
        {
            if (_sensorConnection != null)
            {
                return true;
            }

            // TODO winsock/Socket with IOCP vs. UWP sockets
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
                var asyncOperation = _dataReader.LoadAsync(8);

                // TODO: timeout handling
                if (!asyncOperation.AsTask().Wait(50000))
                {
                    asyncOperation.Close();
                    asyncOperation.Cancel();
                }
                helloPacket = HelloPacket.Read(_dataReader);
                
                // TODO: compare protocol versions

                // Start receive loop
            }
            catch (Exception)
            {
                _sensorConnection?.Dispose();
                _sensorConnection = null;
                _dataReader?.Dispose();
                _dataReader = null;
                _dataWriter?.Dispose();
                _dataWriter = null;

                return false;
            }
            return true;
        }
    }
}
