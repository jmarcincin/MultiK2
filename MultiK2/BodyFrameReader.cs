using System;
using Windows.Media.Capture.Frames;

using MultiK2.Tracking;
using Windows.Foundation;
using System.Threading.Tasks;
using MultiK2.Network;

namespace MultiK2
{
    public sealed class BodyFrameReader
    {
        private readonly MediaFrameReader _bodyReader;
        private readonly NetworkClient _networkClient;

        private bool _isStarted;

        public event EventHandler<BodyFrameArrivedEventArgs> FrameArrived;
        
        public Sensor Sensor { get; }

        internal BodyFrameReader(Sensor sensor, NetworkClient networkClient)
        {
            Sensor = sensor;
            _networkClient = networkClient;
        }
        
        internal BodyFrameReader(Sensor sensor, MediaFrameReader bodyReader)
        {
            Sensor = sensor;
            _bodyReader = bodyReader;    
        }

        private void NetworkClient_BodyFrameArrived(object sender, BodyFramePacket e)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                var bodyArgs = new BodyFrameArrivedEventArgs(this, e.BodyFrame);
                subscribers(this, bodyArgs);
            }
        }

        private void BodyFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                var frame = sender.TryAcquireLatestFrame();
                if (frame != null && frame.BufferMediaFrame?.Buffer != null)
                {
                    var coordinateMapper = frame.CoordinateSystem;

                    var bodyArgs = new BodyFrameArrivedEventArgs(this, BodyFrame.Parse(frame));
                    frame.Dispose();
                    subscribers(this, bodyArgs);
                }
                frame?.Dispose();
            }
        }
        
        public IAsyncOperation<MediaFrameReaderStartStatus> OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (!_isStarted)
                {
                    if (_bodyReader != null)
                    {
                        var status = await _bodyReader.StartAsync();
                        if (status == MediaFrameReaderStartStatus.Success)
                        {
                            _bodyReader.FrameArrived += BodyFrameReader_FrameArrived;
                            _isStarted = true;
                        }
                        return status;
                    }
                    else
                    {
                        // todo status mapping & await for network operation
                        var response = await _networkClient.SendCommandAsync(new OpenReader(ReaderType.Body, ReaderConfig.Default));
                        if (response.Status == OperationStatus.ResponseSuccess)
                        {
                            _networkClient.BodyFrameArrived += NetworkClient_BodyFrameArrived;
                            _isStarted = true;
                        }
                        return response.Status.ToMediaReaderStartStatus();
                    }
                }
                return MediaFrameReaderStartStatus.Success;
            }).AsAsyncOperation();
        }
        
        public IAsyncAction CloseAsync()
        {
            return Task.Run(async () =>
            {
                if (_bodyReader != null)
                {
                    _bodyReader.FrameArrived -= BodyFrameReader_FrameArrived;
                    await _bodyReader.StopAsync();
                }
                else
                {
                    _networkClient.BodyFrameArrived -= NetworkClient_BodyFrameArrived;
                    // todo handle response?
                    await _networkClient.SendCommandAsync(new CloseReader(ReaderType.Body));
                }

                _isStarted = false;
            }).AsAsyncAction();
        }

        internal void Dispose()
        {
            _bodyReader?.Dispose();
        }
    }

    public sealed class BodyFrameArrivedEventArgs
    {
        public BodyFrameReader Source { get; }

        public BodyFrame BodyFrame { get; }

        internal BodyFrameArrivedEventArgs(BodyFrameReader source, BodyFrame bodyFrame)
        {
            Source = source;
            BodyFrame = bodyFrame;
        }        
    }
}
