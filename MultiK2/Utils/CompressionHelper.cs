using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Utils
{
    public static class CompressionHelper
    {
        public static unsafe void CompressDepth(byte* src, byte* dest, uint srcByteCount, out uint destBytesUsed)
        {
            destBytesUsed = 0;
            ushort* srcData = (ushort*)src;

            var occurence = 1;
            ushort lastDepth = srcData[0];
            int srcIndex = 1;
            do
            {
                var depthData = srcData[srcIndex++];
                if (lastDepth == depthData)
                {
                    occurence++;
                }
                else
                {
                    // write cached depth value 
                    destBytesUsed += WriteCompressedValue(dest + destBytesUsed, lastDepth, occurence);

                    lastDepth = depthData;
                    occurence = 1;
                }

                if (occurence > 8)
                {
                    destBytesUsed += WriteCompressedValue(dest + destBytesUsed, lastDepth, 8);
                    occurence = 1;
                }
            } while (srcIndex < srcByteCount / 2);

            destBytesUsed += WriteCompressedValue(dest + destBytesUsed, lastDepth, occurence);
        }

        public static unsafe void DecompressDepth(byte* src, byte* dest, uint srcByteCount, out uint destBytesStored)
        {
            ushort* srcData = (ushort*)src;
            destBytesStored = 0;
            for (var i = 0; i < srcByteCount / 2; i++)
            {
                var compressedValue = srcData[i];
                var depthValue = compressedValue & 0x1FFF;
                var count = (compressedValue >> 13) + 1;

                destBytesStored += WriteUncompressedValue(dest + destBytesStored, depthValue, count);
            }
        }

        private static unsafe uint WriteUncompressedValue(byte* dest, int data, int count)
        {
            for (var i = 0; i < count; i++)
            {
                *dest = (byte)data;
                *(dest + 1) = (byte)((data >> 8));
                dest += 2;
            }
            return 2 * (uint)count;
        }

        private static unsafe uint WriteCompressedValue(byte* dest, ushort data, int count)
        {
            count = count - 1;
            *dest = (byte)data;
            *(dest + 1) = (byte)((data >> 8) | (count << 5));
            return 2;

            /*
            if (count < 2)
            {
                *(dest + 1) = (byte)((data >> 8) | (count << 5));
                return 2;
            }
            else
            {
                // add highest bit flag
                *(dest + 1) = (byte)((data >> 8) & 0x80);
                *(dest + 2) = (byte)count;
                return 3;
            }*/
        }
    }
}
