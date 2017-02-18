using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

using MultiK2;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MultiK2.Network
{
    class DepthFramePacket : FramePacket
    {
        // todo validate if depth data length is always in multiples of 8
        private byte[] _data;
        private IBuffer _dataBuffer;
                
        private uint _offset;

        public SoftwareBitmap Frame { get; }

        public DepthFramePacket() : base(ReaderType.Depth)
        {
            using (var buffer = Frame.LockBuffer(BitmapBufferAccessMode.Read))
            using (var bufferRef = buffer.CreateReference())
            {
                unsafe
                {
                    byte* bufferPtr;
                    uint bufferCapacity;
                    ((IMemoryBufferByteAccess)bufferRef).GetBuffer(out bufferPtr, out bufferCapacity);

                    _data = new byte[bufferCapacity];

                    ulong* sourcePtr = (ulong*)bufferPtr;
                    fixed (byte* dataPtr = _data)
                    {
                        ulong* targetPtr = (ulong*)dataPtr;
                        // todo: memcpy optimization
                        for (var i = 0; i < bufferCapacity / 8; i++)
                        {
                            targetPtr[i] = sourcePtr[i];
                        }
                    }

                    _dataBuffer = _data.AsBuffer();
                }
            }
        }

        public override async Task<bool> WriteData(DataWriter writer)
        {
            if (_dataBuffer == null)
            {
                // TODO: semaphore impl - sending in chunks
                
                writer.WriteInt32((int)OperationCode.FrameTransfer);
                writer.WriteInt32((int)OperationType.PushInit);
                writer.WriteInt32((int)Type);
                writer.WriteInt32((int)Frame.BitmapPixelFormat);
                writer.WriteInt32(Frame.PixelWidth);
                writer.WriteInt32(Frame.PixelHeight);
                writer.WriteInt32(_data.Length);

                // todo write coordinte mapper transformations
                await writer.FlushAsync();

                return false;
            }

            /*
            writer.WriteInt32((int)OperationCode.FrameTransfer);
            writer.WriteInt32((int)OperationType.Push);
            writer.WriteInt32((int)Type);
            writer.WriteInt32((int)Frame.BitmapPixelFormat);
            writer.WriteUInt32(_datal.e);

            // 32k chunks?
            var dataBuffer = _data.AsBuffer();
            writer.WriteBuffer(dataBuffer, 0, 32768);
            await writer.FlushAsync();
            */
            return false;
        }
    }
}
