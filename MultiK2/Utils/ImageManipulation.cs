using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace MultiK2.Utils
{
    internal static class ImageManipulation
    {
        /// <summary>
        /// Resizes the original <see cref="SoftwareBitmap"/> dimensions to half.
        /// </summary>
        /// <param name="source">Source bitmap.</param>
        public unsafe static SoftwareBitmap Downsize(this SoftwareBitmap source)
        {
            // TODO: additional formats & factors support?
            if (source.BitmapPixelFormat != BitmapPixelFormat.Yuy2)
            {
                throw new NotSupportedException();
            }

            var targetBitmap = new SoftwareBitmap(source.BitmapPixelFormat, source.PixelWidth / 2, source.PixelHeight / 2);

            using (var sourceBuffer = source.LockBuffer(BitmapBufferAccessMode.Read))
            using (var targetBuffer = targetBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var sourceBufferRef = sourceBuffer.CreateReference())
            using (var targetBufferRef = targetBuffer.CreateReference())
            {
                byte* sourceBytePtr;
                uint sourceCapacity;

                byte* targetBytePtr;
                uint targetCapacity;

                ((IMemoryBufferByteAccess)sourceBufferRef).GetBuffer(out sourceBytePtr, out sourceCapacity);
                ((IMemoryBufferByteAccess)targetBufferRef).GetBuffer(out targetBytePtr, out targetCapacity);

                uint* sourcePtr = (uint*)sourceBytePtr;
                uint* targetPtr = (uint*)targetBytePtr;

                // todo: evaluate ulong vs uint impl. perf && TPL's Parallel.For impl
                // && bilinear vs point sampling img quality

                var targetOffset = 0;
                for (var y = 0; y < source.PixelHeight; y += 2)
                {
                    // source is expressed in macropixels - 2 pixels sharing U & V (yuv 4:2:2 horizontal downsampling in yuy2 format)
                    // see https://msdn.microsoft.com/en-us/library/windows/desktop/dd206750%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396#YUV422formats16bitsperpixel

                    uint* linePtr = sourcePtr + y * (source.PixelWidth / 2);
                    for (var x = 0; x < source.PixelWidth / 2; x += 2)
                    {
                        // doublepixel format for yuy2: Y0 U0 Y1 V0
                        uint firstMacroPixel = *(linePtr + x);
                        uint secondMacroPixel = *(linePtr + x + 1);

                        // Warning: algorithm written for little endian!!
                        uint y0 = firstMacroPixel & 0xff;
                        uint y1 = (secondMacroPixel & 0xff) << 16;

                        uint firstU = (firstMacroPixel >> 8) & 0xff;
                        uint secondU = (secondMacroPixel >> 8) & 0xff;
                        uint u0 = ((firstU + secondU) / 2) << 8;

                        uint firstV = firstMacroPixel >> 24;
                        uint secondV = secondMacroPixel >> 24;
                        uint v0 = ((firstV + secondV) / 2) << 24;

                        uint downSizedMacroPixel = y0 | u0 | y1 | v0;
                        *(targetPtr + targetOffset) = downSizedMacroPixel;
                        targetOffset++;
                    }
                }
            }

            return targetBitmap;
        }
    }
}
