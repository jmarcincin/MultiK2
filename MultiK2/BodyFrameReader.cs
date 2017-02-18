using System;
using Windows.Media.Capture.Frames;

using MultiK2.Tracking;
using Windows.Foundation;
using System.Threading.Tasks;

namespace MultiK2
{
    public sealed class BodyFrameReader
    {
        private MediaFrameReader _bodyReader;

        private bool _isStarted;

        public event EventHandler<BodyFrameArrivedEventArgs> FrameArrived;
        
        public Sensor Sensor { get; }

        internal BodyFrameReader(Sensor sensor, MediaFrameReader bodyReader)
        {
            Sensor = sensor;
            _bodyReader = bodyReader;
            _bodyReader.FrameArrived += BodyFrameReader_FrameArrived;            
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
            }
        }
        
        public IAsyncOperation<MediaFrameReaderStartStatus> OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (!_isStarted)
                {
                    var status = await _bodyReader.StartAsync();
                    if (status == MediaFrameReaderStartStatus.Success)
                    {
                        _bodyReader.FrameArrived += BodyFrameReader_FrameArrived;
                        _isStarted = true;
                    }
                    return status;
                }
                return MediaFrameReaderStartStatus.Success;
            }).AsAsyncOperation();
        }

        public IAsyncAction CloseAsync()
        {
            return Task.Run(async () =>
            {
                _bodyReader.FrameArrived -= BodyFrameReader_FrameArrived;
                await _bodyReader.StopAsync();
                _isStarted = false;
            }).AsAsyncAction();
        }

        internal void Dispose()
        {
            _bodyReader?.Dispose();
            _bodyReader = null;
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
