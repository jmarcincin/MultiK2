using System;
using System.Numerics;
using Windows.Graphics.Imaging;
using MultiK2.Utils;

namespace MultiK2.Network
{
    internal class BodyIndexFramePacket : FramePacket
    {
        // todo validate if depth data length is always in multiples of 8
        private byte[] _data;                
        private int _offset;

        public SoftwareBitmap Bitmap { get; private set; }

        public CameraIntrinsics CameraIntrinsics { get; private set; }

        public Matrix4x4? DepthToColorTransform { get; private set; }

        public BodyIndexFramePacket(SoftwareBitmap bodyIndexBitmap, CameraIntrinsics intrinsics, Matrix4x4? depthToColorTransform) : base(ReaderType.BodyIndex)
        {
            Bitmap = bodyIndexBitmap;
            CameraIntrinsics = intrinsics;
            DepthToColorTransform = depthToColorTransform;
        }

        public BodyIndexFramePacket() : base(ReaderType.BodyIndex) { }

        public override bool WriteData(WriteBuffer writer)
        {
            if (_data == null)
            {
                using (var buffer = Bitmap.LockBuffer(BitmapBufferAccessMode.Read))
                using (var bufferRef = buffer.CreateReference())
                {
                    unsafe
                    {
                        byte* bufferPtr;
                        uint bufferCapacity;
                        ((IMemoryBufferByteAccess)bufferRef).GetBuffer(out bufferPtr, out bufferCapacity);

                        _data = new byte[bufferCapacity];

                        fixed (byte* dataPtr = _data)
                        {
                            DataManipulation.Copy(bufferPtr, dataPtr, bufferCapacity);
                        }
                    }
                }
                
                writer.Write((int)OperationCode.BodyIndexFrameTransfer);
                writer.Write((int)OperationStatus.PushInit);
                writer.Write((int)Bitmap.BitmapPixelFormat);
                writer.Write(Bitmap.PixelWidth);
                writer.Write(Bitmap.PixelHeight);
                writer.Write(_data.Length);

                // camera intrinsics
                WriteIntrinsics(writer, CameraIntrinsics);

                // todo write transformation
                WriteTransformation(writer, DepthToColorTransform);

                return false;
            }

            writer.Write((int)OperationCode.BodyIndexFrameTransfer);
            writer.Write((int)OperationStatus.Push);

            // just for check?
            writer.Write(_offset);

            // todo configurable chunks size support
            var dataChunkSize = Math.Min(_data.Length - _offset, writer.RemainingPacketWriteCapacity - 4);
            writer.Write(dataChunkSize);

            // account for 4 bytes taken by chunkSize info!!

            int writeOffset;
            writer.ReserveForWrite(dataChunkSize, out writeOffset);
            unsafe
            {
                var buffer = writer.GetBuffer();
                fixed (byte* bufferPtr = buffer)
                fixed (byte* dataSourcePtr = _data)
                {
                    DataManipulation.Copy(dataSourcePtr + _offset, bufferPtr + writeOffset, (uint)dataChunkSize);
                }
            }
            
            _offset += dataChunkSize;
            return _offset == _data.Length;
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
                DepthToColorTransform = ReadTransformation(reader);

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
