using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2
{
    public sealed class CameraIntrinsics
    {
        public float FocalLengthX { get; set; }
        public float FocalLengthY { get; set; }
        public float PrincipalPointX { get; set; }
        public float PrincipalPointY { get; set; }
        public float RadialDistortionFourthOrder { get; set; }
        public float RadialDistortionSecondOrder { get; set; }
        public float RadialDistortionSixthOrder { get; set; }

        public float FrameWidth { get; }
        public float FrameHeight { get; }

        public Windows.Media.Devices.Core.CameraIntrinsics OriginalIntrinsics { get; }

        internal CameraIntrinsics(Windows.Media.Devices.Core.CameraIntrinsics rtCi)
        {
            OriginalIntrinsics = rtCi;

            FrameWidth = rtCi.ImageWidth;
            FrameHeight = rtCi.ImageHeight;

            FocalLengthX = rtCi.FocalLength.X;
            FocalLengthY = rtCi.FocalLength.Y;
            PrincipalPointX = rtCi.PrincipalPoint.X;

            // TODO: validate - 'inverted' principal point to fit all existing kinect algorithms & Kinect SDK: SDK_Cy = frame_height - RT_Cy
            // (who had a great idea to break formulas used for a decade in 'CV using kinect' papers?)
            PrincipalPointY = FrameHeight - rtCi.PrincipalPoint.Y;
            RadialDistortionSecondOrder = rtCi.RadialDistortion.X;
            RadialDistortionFourthOrder = rtCi.RadialDistortion.Y;
            RadialDistortionSixthOrder = rtCi.RadialDistortion.Z;            
        }

        internal CameraIntrinsics(
            float focalX,
            float focalY, 
            float height,
            float width, 
            float principalX, 
            float pricipalY,
            float rad2, 
            float rad4, 
            float rad6)
        {
            FocalLengthX = focalX;
            FocalLengthY = focalY;
            FrameHeight = height;
            FrameWidth = width;

            PrincipalPointX = principalX;
            PrincipalPointY = pricipalY;

            RadialDistortionSecondOrder = rad2;
            RadialDistortionFourthOrder = rad4;
            RadialDistortionSixthOrder = rad6;
        }

        public Vector2 ProjectOntoFrame(Vector3 cameraPoint)
        {
            // corrected mix of https://social.msdn.microsoft.com/Forums/en-US/b35038d1-e711-4aa2-a1de-bc4eb7270cc7/radial-distortion-correction?forum=kinectv2sdk
            // and https://social.msdn.microsoft.com/Forums/en-US/9e3bbba8-5412-47b5-89e4-d5d684fc45db/mapdepthframetocameraspace-using-depthcameraintrinsics-problem?forum=kinectv2sdk
                       
            var u = FocalLengthX * cameraPoint.X / cameraPoint.Z + PrincipalPointX;
            var v = PrincipalPointY - (FocalLengthY * cameraPoint.Y / cameraPoint.Z);

            // distort 
            u = (u - PrincipalPointX) / FocalLengthX;
            v = (PrincipalPointY - v) / FocalLengthY;

            var r = u * u + v * v;
            var d = 1 + RadialDistortionSecondOrder * r + RadialDistortionFourthOrder * r * r + RadialDistortionSixthOrder * r * r * r;

            u = u * d * FocalLengthX + PrincipalPointX;
            v = PrincipalPointY - v * d * FocalLengthY;
            
            return new Vector2(u, v);
        }

        public Vector3 UnprojectFromFrame(Vector2 coordinate, float depth = 1)
        {
            var x = (coordinate.X - PrincipalPointX) / FocalLengthX;
            var y = (PrincipalPointY - coordinate.Y) / FocalLengthY;

            // undistort
            var r = x * x + y * y;
            var d = 1 - RadialDistortionSecondOrder * r - RadialDistortionFourthOrder * r * r - RadialDistortionSixthOrder * r * r * r;

            // Camera space reports coordinates in meters vs. millimeters (e.g. depth image)
            x = x * d * depth;
            y = y * d * depth;
            
            return new Vector3(x, y, depth);
        }
    }
}
