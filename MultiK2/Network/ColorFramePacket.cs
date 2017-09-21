using System;
using System.Numerics;
using Windows.Graphics.Imaging;
using MultiK2.Utils;

namespace MultiK2.Network
{
    internal class ColorFramePacket : FramePacket
    {
        private int _dataCapacity;
        private int _offset;
        private bool _init = true;

        public SoftwareBitmap Bitmap { get; private set; }

        public CameraIntrinsics CameraIntrinsics { get; private set; }

        public Matrix4x4? ColorToDepthTransform { get; private set; }

        public ColorFramePacket(SoftwareBitmap colorBitmap, CameraIntrinsics intrinsics, Matrix4x4? colorToDepthTransform) : base(ReaderType.Color)
        {
            Bitmap = colorBitmap;
            CameraIntrinsics = intrinsics;
            ColorToDepthTransform = colorToDepthTransform;
        }

        public ColorFramePacket() : base(ReaderType.Color) { }

        public override bool WriteData(WriteBuffer writer)
        {
            if (_init)
            {
                // downsize?
                // Bitmap = Bitmap.Downsize();

                // todo: handle different pixelformats
                _dataCapacity = Bitmap.PixelHeight * Bitmap.PixelWidth * 2;

                writer.Write((int)OperationCode.ColorFrameTransfer);
                writer.Write((int)OperationStatus.PushInit);
                writer.Write((int)Bitmap.BitmapPixelFormat);
                writer.Write(Bitmap.PixelWidth);
                writer.Write(Bitmap.PixelHeight);
                writer.Write(_dataCapacity);

                // camera intrinsics
                WriteIntrinsics(writer, CameraIntrinsics);

                // write transformation
                WriteTransformation(writer, ColorToDepthTransform);
                _init = false;

                return false;
            }

            writer.Write((int)OperationCode.ColorFrameTransfer);
            writer.Write((int)OperationStatus.Push);

            // just for check?
            writer.Write(_offset);

            // todo configurable chunks size support
            // account for 4 bytes taken by chunkSize info!!
            var dataChunkSize = Math.Min(_dataCapacity - _offset, writer.RemainingPacketWriteCapacity - 4);
            writer.Write(dataChunkSize);

            int writeOffset;
            writer.ReserveForWrite(dataChunkSize, out writeOffset);
            unsafe
            {
                using (var bitBuffer = Bitmap.LockBuffer(BitmapBufferAccessMode.Read))
                using (var bufferRef = bitBuffer.CreateReference())
                {
                    byte* dataSourcePtr;
                    uint bufferCapacity;
                    ((IMemoryBufferByteAccess)bufferRef).GetBuffer(out dataSourcePtr, out bufferCapacity);

                    var buffer = writer.GetBuffer();
                    fixed (byte* bufferPtr = buffer)
                    {
                        DataManipulation.Copy(dataSourcePtr + _offset, bufferPtr + writeOffset, (uint)dataChunkSize);
                    }
                }
            }
            _offset += dataChunkSize;
            return _offset == _dataCapacity;
        }

        public override bool ReadData(ReadBuffer reader)
        {
            if (Bitmap == null)
            {
                // header + CI + transformation mx
                var status = (OperationStatus)reader.ReadInt32();
                var pixelFormat = (BitmapPixelFormat)reader.ReadInt32();
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();
                var bitmapSize = reader.ReadInt32();
                
                Bitmap = new SoftwareBitmap(pixelFormat, width, height, BitmapAlphaMode.Ignore);

                CameraIntrinsics = ReadCameraIntrinsics(reader);
                ColorToDepthTransform = ReadTransformation(reader);

                return false;
            }
            
            var operationStatus = (OperationStatus)reader.ReadInt32();

            // check?
            var offset = reader.ReadInt32();
            var dataLength = reader.ReadInt32();

            int readOffset;
            reader.ReserveForReading(dataLength, out readOffset);

            uint bufferCapacity;
            using (var buffer = Bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var bufferRef = buffer.CreateReference())
            {
                unsafe
                {
                    byte* targetPtr;
                    ((IMemoryBufferByteAccess)bufferRef).GetBuffer(out targetPtr, out bufferCapacity);

                    targetPtr += _offset;
                    var readBuffer = reader.GetBuffer();
                    fixed (byte* readBufferPtr = readBuffer)
                    {
                        DataManipulation.Copy(readBufferPtr + readOffset, targetPtr, (uint)dataLength);
                        _offset += dataLength;
                    }
                }
            }
            return _offset == bufferCapacity;
        }
    }
}
