using MultiK2.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    internal abstract class FramePacket
    {
        public ReaderType FrameType { get; }

        protected FramePacket(ReaderType type)
        {
            FrameType = type;
        }

        public abstract bool WriteData(WriteBuffer writer);

        public abstract bool ReadData(ReadBuffer reader);

        /// <summary>
        /// 36B
        /// </summary>
        protected void WriteIntrinsics(WriteBuffer writer, CameraIntrinsics intrinsics)
        {
            writer.Write(intrinsics.FocalLengthX);
            writer.Write(intrinsics.FocalLengthY);
            writer.Write(intrinsics.FrameHeight);
            writer.Write(intrinsics.FrameWidth);

            writer.Write(intrinsics.PrincipalPointX);
            writer.Write(intrinsics.PrincipalPointY);

            writer.Write(intrinsics.RadialDistortionSecondOrder);
            writer.Write(intrinsics.RadialDistortionFourthOrder);
            writer.Write(intrinsics.RadialDistortionSixthOrder);
        }
         /// <summary>
         /// 36B
         /// </summary>
         protected CameraIntrinsics ReadCameraIntrinsics(ReadBuffer reader)
         {
            var focalX = reader.ReadSingle();
            var focalY = reader.ReadSingle();
            var height = reader.ReadSingle();
            var width = reader.ReadSingle();

            var principalX = reader.ReadSingle();
            var principalY = reader.ReadSingle();

            var rad2 = reader.ReadSingle();
            var rad4 = reader.ReadSingle();
            var rad6 = reader.ReadSingle();

            return new CameraIntrinsics(
                focalX,
                focalY,
                height,
                width,
                principalX,
                principalY,
                rad2,
                rad4,
                rad6);
        }

        /// <summary>
        /// 64B + 4B flag
        /// </summary>
        protected void WriteTransformation(WriteBuffer writer, Matrix4x4? transformation)
        {
            writer.Write(transformation.HasValue ? 1 : 0);

            // 4 * 4 * sizeof(float) 
            int writeOffset;

            // todo: check for fail of reservation?
            writer.ReserveForWrite(64, out writeOffset);
            if (transformation.HasValue)
            {
                var transMatrix = transformation.Value;
                var buffer = writer.GetBuffer();
                unsafe
                {
                    fixed (byte* bufferPtr = buffer)
                    {                        
                        byte* transformationPtr = (byte*)&transMatrix;
                        DataManipulation.Copy(transformationPtr, bufferPtr + writeOffset, 64);
                    }
                }
            }
        }

        /// <summary>
        /// 64B + 4B flag
        /// </summary>
        protected Matrix4x4? ReadTransformation(ReadBuffer reader)
        {
            var hasValue = reader.ReadInt32() == 1;

            // 4 * 4 * sizeof(float) 
            int readOffset;
            reader.ReserveForReading(64, out readOffset);

            if (!hasValue)
            {
                return null;
            }
            
            unsafe
            {
                var buffer = reader.GetBuffer();
                fixed (byte* bufferPtr = buffer)
                {
                    Matrix4x4 result;
                    byte* resultMatrixPtr = (byte*)&result;
                    DataManipulation.Copy(bufferPtr + readOffset, resultMatrixPtr, 64);

                    return result;
                }
            }
        }
    }
}
