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
    public sealed class BodyIndexFrameReader
    {
        private MediaFrameReader _bodyIndexReader;

        private bool _isStarted;

        public event EventHandler<BodyIndexFrameArrivedEventArgs> FrameArrived;
                
        public Sensor Sensor { get; }

        internal BodyIndexFrameReader(Sensor sensor, MediaFrameReader bodyIndexReader)
        {
            Sensor = sensor;
            _bodyIndexReader = bodyIndexReader;
        }

        private void BodyIndexReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var subscribers = FrameArrived;
            if (subscribers != null)
            {
                var frame = sender.TryAcquireLatestFrame();
                if (frame != null)
                {

                    var bodyIndexArgs = 
                        new BodyIndexFrameArrivedEventArgs(
                            this, 
                            frame.VideoMediaFrame.SoftwareBitmap, 
                            new CameraIntrinsics(frame.VideoMediaFrame.CameraIntrinsics));

                    subscribers(this, bodyIndexArgs);
                }
            }
        }

        public IAsyncOperation<MediaFrameReaderStartStatus> OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (!_isStarted)
                {
                    var status = await _bodyIndexReader.StartAsync();
                    if (status == MediaFrameReaderStartStatus.Success)
                    {
                        _bodyIndexReader.FrameArrived += BodyIndexReader_FrameArrived;
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
                _bodyIndexReader.FrameArrived -= BodyIndexReader_FrameArrived;
                await _bodyIndexReader.StopAsync();
                _isStarted = false;
            }).AsAsyncAction();
        }

        internal void Dispose()
        {
            _bodyIndexReader?.Dispose();
            _bodyIndexReader = null;
        }
    }

    public sealed class BodyIndexFrameArrivedEventArgs
    {
        private static readonly uint[] ColorMap = { 0xffff0000, 0xff00ff00, 0xff0000ff, 0xffffff00, 0xff00ffff, 0xffff00ff };

        public BodyIndexFrameReader Source { get; }

        public SoftwareBitmap Bitmap { get; }

        public CameraIntrinsics CameraIntrinsics { get; }

        internal BodyIndexFrameArrivedEventArgs(BodyIndexFrameReader source, SoftwareBitmap bitmap, CameraIntrinsics intrinsics)
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
                                
                uint* targetDataPtr = (uint*)targetPtr;

                for (var i = 0; i < sourceCapacity; i++)
                {
                    byte index = sourcePtr[i];
                    
                    targetDataPtr[i] = index == 0xff ? 0 : ColorMap[index];                                        
                }
            }
            
            return targetBitmap;
        }
    }
}
