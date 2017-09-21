
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiK2.Utils;

namespace BufferTests
{
    [TestClass]
    public class CompressionTests
    {
        [TestMethod]
        public void DepthCompression_ValidOutput()
        {
            var input = new ushort[] { 2048, 2049, 0, 0, 0, 4097, 4097 };

            // 1*2048, 1*2049, 3*0, 2*4097
            var expectedOutput = new byte[] { 0, 8, 1, 8, 0, 0 | 0x40, 1, 16 | 0x20 };

            byte[] producedOutput = new byte[input.Length * 2];
            
            uint usedBytes;
            unsafe
            {
                fixed (ushort* inputPtr = input)
                fixed (byte* outputPtr = producedOutput)
                {
                    CompressionHelper.CompressDepth((byte*)inputPtr, outputPtr, (uint)input.Length * 2, out usedBytes);
                }
            }

            Assert.AreEqual(expectedOutput.Length, (int)usedBytes);
            Assert.IsTrue(CompareBlobs(expectedOutput, producedOutput, (int)usedBytes));
        }

        [TestMethod]
        public void DepthDecompression_ValidOutput()
        {
            // 1*2048, 1*2049, 3*0, 2*4097
            var compressedInput = new byte[] { 0, 8, 1, 8, 0, 0 | 0x40, 1, 16 | 0x20 };
            
            // var expectedOutput = new ushort[] { 2048, 2049, 0, 0, 0, 4097, 4097 };
            var expectedOutput = new byte[] { 0, 8, 1, 8, 0, 0, 0, 0, 0, 0, 1, 16, 1, 16 };
            byte[] producedOutput = new byte[expectedOutput.Length];
            
            uint decBytesStored;
            unsafe
            {
                fixed (byte* inputPtr = compressedInput)
                fixed (byte* outputPtr = producedOutput)
                {
                    CompressionHelper.DecompressDepth(inputPtr, outputPtr, (uint)compressedInput.Length, out decBytesStored);
                }
            }

            Assert.AreEqual(expectedOutput.Length, (int)decBytesStored);
            Assert.IsTrue(CompareBlobs(expectedOutput, producedOutput, (int)decBytesStored));
        }

        [TestMethod]
        public void DepthCompression_RepeatOverflow_ValidOutput()
        {
            // >8 repeats of the same value
            var input = new ushort[] { 2048, 2049, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4097, 4097 };

            // 1*2048, 1*2049, 8*0, 1*0, 2*4097
            var expectedOutput = new byte[] { 0, 8, 1, 8, 0, 0 | 0xE0, 0, 0, 1, 16 | 0x20 };

            byte[] producedOutput = new byte[input.Length * 2];

            uint usedBytes;
            unsafe
            {
                fixed (ushort* inputPtr = input)
                fixed (byte* outputPtr = producedOutput)
                {
                    CompressionHelper.CompressDepth((byte*)inputPtr, outputPtr, (uint)input.Length * 2, out usedBytes);
                }
            }

            Assert.AreEqual(expectedOutput.Length, (int)usedBytes);
            Assert.IsTrue(CompareBlobs(expectedOutput, producedOutput, (int)usedBytes));
        }
        
        private bool CompareBlobs(byte[] blob1, byte[] blob2, int count)
        {
            if (blob1.Length < count || blob2.Length < count)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (blob1[i] != blob2[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
