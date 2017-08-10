using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Utils
{
    internal class DataManipulation
    {
        public static unsafe void Copy(byte* source, byte* destination, uint count)
        {
            var blocks = count / 16;
            Vector4* sourceVec = (Vector4*)source;
            Vector4* targetVec = (Vector4*)destination;

            for (var i = 0; i < blocks; i++)
            {
                targetVec[i] = sourceVec[i];
            }
            
            for (var i = blocks * 16; i < count; i++)
            {
                destination[i] = source[i]; 
            }

            // 64B unroll
            /*
            var simdCopies = count / 64;
            if (simdCopies > 0)
            {
                Vector4* sourceVec = (Vector4*)source;
                Vector4* targetVec = (Vector4*)target;
                for (var i = 0; i < simdCopies; i++)
                {
                    targetVec[i] = sourceVec[i];
                    targetVec[++i] = sourceVec[i];
                    targetVec[++i] = sourceVec[i];
                    targetVec[++i] = sourceVec[i];
                }
            }

            var remainingCopies = count % 64;
            if (remainingCopies > 0)
            {
                var offset = simdCopies * 64;
                source = source + offset;
                target = target + offset;

                for (var i = 0; i < remainingCopies; i++)
                {
                    target[i] = source[i];
                }
            }*/
        }
    }
}
