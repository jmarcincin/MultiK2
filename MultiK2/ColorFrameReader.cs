using MultiK2.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;

namespace MultiK2
{
    public sealed class ColorFrameReader
    {
        private readonly MediaFrameReader _colorReader;
        private readonly NetworkClient _networkClient;

        private bool _isStarted;

        public event EventHandler<ColorFrameArrivedEventArgs> FrameArrived;
        
        public Sensor Sensor { get; }

        public ReaderConfig ReaderConfiguration { get; }

        internal ColorFrameReader(Sensor sensor, NetworkClient networkClient)
        {
            Sensor = sensor;
            _networkClient = networkClient;
        }

        internal ColorFrameReader(Sensor sensor, MediaFrameReader colorReader, ReaderConfig config)
        {
            Sensor = sensor;
            ReaderConfiguration = config;

            _colorReader = colorReader;
        }

        private void NetworkClient_ColorFrameArrived(object sender, ColorFramePacket e)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                Sensor.GetCoordinateMapper().ColorToDepth = e.ColorToDepthTransform;
                var colorArgs =
                        new ColorFrameArrivedEventArgs(
                            this,
                            e.Bitmap,
                            e.CameraIntrinsics);

                subscribers(this, colorArgs);
            }
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
                frame?.Dispose();
            }
        }

        public IAsyncOperation<MediaFrameReaderStartStatus> OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (!_isStarted)
                {
                    if (_colorReader != null)
                    {
                        var status = await _colorReader.StartAsync();
                        if (status == MediaFrameReaderStartStatus.Success)
                        {
                            _colorReader.FrameArrived += ColorReader_FrameArrived;
                            _isStarted = true;
                        }
                        return status;
                    }
                    else
                    {
                        var response = await _networkClient.SendCommandAsync(new OpenReader(ReaderType.Color, ReaderConfig.Default));
                        if (response.Status == OperationStatus.ResponseSuccess)
                        {
                            _networkClient.ColorFrameArrived += NetworkClient_ColorFrameArrived;
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
                if (_colorReader != null)
                {
                    _colorReader.FrameArrived -= ColorReader_FrameArrived;
                    await _colorReader.StopAsync();
                }
                else
                {
                    _networkClient.ColorFrameArrived -= NetworkClient_ColorFrameArrived;
                    await _networkClient.SendCommandAsync(new CloseReader(ReaderType.Color));
                }
                _isStarted = false;
            }).AsAsyncAction();            
        }

        internal void Dispose()
        {
            _colorReader?.Dispose();
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
