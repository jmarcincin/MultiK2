using MultiK2.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;

namespace MultiK2
{
    public sealed class DepthFrameReader
    {
        private readonly MediaFrameReader _depthReader;
        private readonly NetworkClient _networkClient;

        private bool _isStarted;

        public event EventHandler<DepthFrameArrivedEventArgs> FrameArrived;
                
        public Sensor Sensor { get; }

        internal DepthFrameReader(Sensor sensor, NetworkClient networkClient)
        {
            Sensor = sensor;
            _networkClient = networkClient;
        }

        internal DepthFrameReader(Sensor sensor, MediaFrameReader depthReader)
        {
            Sensor = sensor;
            _depthReader = depthReader;            
        }
        
        private void NetworkClient_DepthFrameArrived(object sender, DepthFramePacket e)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                Sensor.GetCoordinateMapper().DepthToColor = e.DepthToColorTransform;
                var depthArgs =
                        new DepthFrameArrivedEventArgs(
                            this,
                            e.Bitmap,
                            e.CameraIntrinsics);

                subscribers(this, depthArgs);
            }
        }

        private void DepthReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                var frame = sender.TryAcquireLatestFrame();
                if (frame != null)
                {
                    Sensor.GetCoordinateMapper().UpdateFromDepth(frame.CoordinateSystem);
                    var depthArgs = 
                        new DepthFrameArrivedEventArgs(
                            this, 
                            frame.VideoMediaFrame.SoftwareBitmap, 
                            new CameraIntrinsics(frame.VideoMediaFrame.CameraIntrinsics));

                    subscribers(this, depthArgs);
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
                    if (_depthReader != null)
                    {
                        var status = await _depthReader.StartAsync();
                        if (status == MediaFrameReaderStartStatus.Success)
                        {
                            _depthReader.FrameArrived += DepthReader_FrameArrived;
                            _isStarted = true;
                        }
                        return status;
                    }
                    else
                    {
                        var response = await _networkClient.SendCommandAsync(new OpenReader(ReaderType.Depth, ReaderConfig.Default));
                        if (response.Status == OperationStatus.ResponseSuccess)
                        {
                            _networkClient.DepthFrameArrived += NetworkClient_DepthFrameArrived;
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
                if (_depthReader != null)
                {
                    _depthReader.FrameArrived -= DepthReader_FrameArrived;
                    await _depthReader.StopAsync();
                }
                else
                {
                    _networkClient.DepthFrameArrived -= NetworkClient_DepthFrameArrived;
                    await _networkClient.SendCommandAsync(new CloseReader(ReaderType.Depth));
                }
                _isStarted = false;
            }).AsAsyncAction();
        }

        internal void Dispose()
        {
            _depthReader?.Dispose();
        }
    }

    public sealed class DepthFrameArrivedEventArgs
    {
        public DepthFrameReader Source { get; }

        public SoftwareBitmap Bitmap { get; }

        public CameraIntrinsics CameraIntrinsics { get; }

        internal DepthFrameArrivedEventArgs(DepthFrameReader source, SoftwareBitmap bitmap, CameraIntrinsics intrinsics)
        {
            Source = source;
            Bitmap = bitmap;
            CameraIntrinsics = intrinsics;
        }

        /// <summary>
        /// For DEBUG purposes only. Implementation / Output may change in the future.
        /// </summary>
        public unsafe SoftwareBitmap GetDisplayableBitmap()
        {            
            var targetBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Bitmap.PixelWidth, Bitmap.PixelHeight, BitmapAlphaMode.Ignore);

            using (var sourceBuffer = Bitmap.LockBuffer(BitmapBufferAccessMode.Read))
            using (var targetBuffer = targetBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var sourceBufferRef = sourceBuffer.CreateReference())
            using (var targetBufferRef = targetBuffer.CreateReference())
            {
                byte* sourcePtr;
                uint sourceCapacity;

                byte* targetPtr;
                uint targetCapacity;

                ((IMemoryBufferByteAccess)sourceBufferRef).GetBuffer(out sourcePtr, out sourceCapacity);
                ((IMemoryBufferByteAccess)targetBufferRef).GetBuffer(out targetPtr, out targetCapacity);

                ushort* sourceDataPtr = (ushort*)sourcePtr;
                uint* targetDataPtr = (uint*)targetPtr;

                for (var i = 0; i < sourceCapacity / 2; i++)
                {
                    uint depth = sourceDataPtr[i];
                       
                    // data in millimeters - TODO skip near limit zone + cap at 4 ( /10 ) or 8 meters ( /20) (value capped at /20 - 8192 mm atm)
                    // real max registered depth ~8000mm  
                    // we must fit scaled value into 8 bit value (range 0 - 255)
                    // BGRA uniform grey "shade" broadcast for BGR, Alpha set to ignore in bitmap constructor
                    var depthInverse = (0xfff - depth);
                    var color = (0x00010101u * (0xff - ((depth >> 2) & 0xff)))  /*((depthInverse) >> 4))*/ | 0xff000000;
                    targetDataPtr[i] = color;
                }
            }

            return targetBitmap;
        } 
    }
}
