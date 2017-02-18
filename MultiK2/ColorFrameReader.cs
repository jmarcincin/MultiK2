using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;

namespace MultiK2
{
    public sealed class ColorFrameReader
    {
        private MediaFrameReader _colorReader;

        private bool _isStarted;

        public event EventHandler<ColorFrameArrivedEventArgs> FrameArrived;
        
        public Sensor Sensor { get; }

        public ReaderConfig ReaderConfiguration { get; }

        internal ColorFrameReader(Sensor sensor, MediaFrameReader colorReader, ReaderConfig config)
        {
            Sensor = sensor;
            ReaderConfiguration = config;

            _colorReader = colorReader;
        }

        private void ColorReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                var frame = sender.TryAcquireLatestFrame();
                if (frame != null)
                {
                    Sensor.GetCoordinateMapper().UpdateFromColor(frame.CoordinateSystem);
                    var colorArgs = 
                        new ColorFrameArrivedEventArgs(
                            this,
                            frame.VideoMediaFrame.SoftwareBitmap,
                            new CameraIntrinsics(frame.VideoMediaFrame.CameraIntrinsics));

                    subscribers(this, colorArgs);
                }
            }
        }

        public IAsyncOperation<MediaFrameReaderStartStatus> OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (!_isStarted)
                {
                    var status = await _colorReader.StartAsync();
                    if (status == MediaFrameReaderStartStatus.Success)
                    {
                        _colorReader.FrameArrived += ColorReader_FrameArrived;
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
                _colorReader.FrameArrived -= ColorReader_FrameArrived;
                await _colorReader.StopAsync();
                _isStarted = false;
            }).AsAsyncAction();            
        }

        internal void Dispose()
        {
            _colorReader?.Dispose();
            _colorReader = null;
        }
    }

    public sealed class ColorFrameArrivedEventArgs
    {
        public ColorFrameReader Source { get; }

        public SoftwareBitmap Bitmap { get; }

        public CameraIntrinsics CameraIntrinsics { get; }

        internal ColorFrameArrivedEventArgs(ColorFrameReader source, SoftwareBitmap bitmap, CameraIntrinsics intrinsics)
        {
            Source = source;
            Bitmap = bitmap;
            CameraIntrinsics = intrinsics;
        }

        /// <summary>
        /// For DEBUG purposes only. Implementation / Output may change in the future.
        /// </summary>
        public SoftwareBitmap GetDisplayableBitmap(BitmapPixelFormat pixelFormat)
        {
            return Bitmap.BitmapPixelFormat != pixelFormat ?
                SoftwareBitmap.Convert(Bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore) :
                Bitmap;
        }
    }
}
