using MultiK2.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace MultiK2.Controls
{
    public sealed class SkeletonCanvas : Canvas
    {
        public SkeletonCanvas()
        {            
        }

        /// <summary>
        /// For DEBUG purposes only. Implementation / Output may change in the future.
        /// </summary>
        public void FillBodies(IEnumerable<Body> bodies, CameraIntrinsics cameraIntrinsics, Func<Vector3, Vector3> coordinateTransformation)
        {
            Children.Clear();
            
            if (bodies == null)
            {
                return;
            }

            foreach (var body in bodies.Where(b => b.IsTracked))
            {
                var brush = new SolidColorBrush(Colors.Green);
                var xRatio = ActualWidth / cameraIntrinsics.FrameWidth;
                var yRatio = ActualHeight / cameraIntrinsics.FrameHeight;

                //create skeleton
                foreach (var bone in body.CreateSkeleton().Where(bone => bone.TrackingState == TrackingState.Tracked))
                {
                    var colorSpace = coordinateTransformation(bone.Joint1.Position);
                    /*                    
                    var origColorFramePoint = cameraIntrinsics.OriginalIntrinsics.ProjectOntoFrame(colorSpace);
                    var distortedOrig = cameraIntrinsics.OriginalIntrinsics.DistortPoint(origColorFramePoint);
                    */
                    var colorFramePoint = cameraIntrinsics.ProjectOntoFrame(colorSpace);
                    var unprojectedPoint = cameraIntrinsics.UnprojectFromFrame(colorFramePoint, colorSpace.Z);

                    var line = new Line();
                    line.StrokeThickness = 4;
                    line.Stroke = brush;
                    
                    line.X1 = colorFramePoint.X * xRatio;
                    line.Y1 = colorFramePoint.Y * yRatio;

                    colorSpace = coordinateTransformation(bone.Joint2.Position);
                    colorFramePoint = cameraIntrinsics.ProjectOntoFrame(colorSpace);

                    line.X2 = colorFramePoint.X * xRatio;
                    line.Y2 = colorFramePoint.Y * yRatio;
                    
                    Children.Add(line);
                }

                // track hands
                TrackHand(body.Joints[JointType.HandRight], body.HandStateRight, cameraIntrinsics, coordinateTransformation, xRatio, yRatio);
                TrackHand(body.Joints[JointType.HandLeft], body.HandStateLeft, cameraIntrinsics, coordinateTransformation, xRatio, yRatio);

                // clipped edges
                /*
                DrawClipEdge(body.ClippedEdges & FrameEdges.Top);
                DrawClipEdge(body.ClippedEdges & FrameEdges.Bottom);
                DrawClipEdge(body.ClippedEdges & FrameEdges.Left);
                DrawClipEdge(body.ClippedEdges & FrameEdges.Right);
                */
            }
        }

        private void DrawClipEdge(FrameEdges edgeFlag)
        {
            if (edgeFlag == FrameEdges.None)
            {
                return;
            }

            var clipBrush = new SolidColorBrush(Colors.Red);

            var clipEdge = new Rectangle();
            clipEdge.Fill = clipBrush;

            switch (edgeFlag)
            {
                case FrameEdges.Top:
                    clipEdge.VerticalAlignment = VerticalAlignment.Top;
                    clipEdge.Height = 10;
                    clipEdge.Width = ActualWidth;
                    break;
                case FrameEdges.Bottom:
                    clipEdge.VerticalAlignment = VerticalAlignment.Bottom;
                    clipEdge.Height = 10;
                    clipEdge.Width = ActualWidth;
                    break;
                case FrameEdges.Left:
                    // color / depth frames are mirrored?
                    clipEdge.HorizontalAlignment = HorizontalAlignment.Right;
                    clipEdge.Width = 10;
                    clipEdge.Height = ActualHeight;
                    break;
                case FrameEdges.Right:
                    clipEdge.HorizontalAlignment = HorizontalAlignment.Left;
                    clipEdge.Width = 10;
                    clipEdge.Height = ActualHeight;
                    break;
            }

            Children.Add(clipEdge);
        }

        private void TrackHand(Joint handJoint, HandState state, CameraIntrinsics intrinsics, Func<Vector3, Vector3> coordinateTransformation, double xRatio, double yRatio)
        {
            if (handJoint.PositionTrackingState != TrackingState.Tracked)
            {
                return;
            }

            SolidColorBrush handBrush = null;

            // add hand tracking
            switch (state)
            {
                case HandState.Open:
                    handBrush = new SolidColorBrush(Colors.Blue);
                    break;
                case HandState.Lasso:
                    handBrush = new SolidColorBrush(Colors.Green);
                    break;
                case HandState.Closed:
                    handBrush = new SolidColorBrush(Colors.Red);
                    break;
            }

            if (handBrush != null)
            {
                var circle = new Ellipse();
                circle.Width = 40;
                circle.Height = 40;
                circle.Opacity = 50;
                circle.Fill = handBrush;
                Children.Add(circle);

                var colorSpace = coordinateTransformation(handJoint.Position);
                var colorFramePoint = intrinsics.ProjectOntoFrame(colorSpace);

                // TODO: attached propertis do no set the center of the elipse
                SetLeft(circle, colorFramePoint.X * xRatio - 20);
                SetTop(circle, colorFramePoint.Y * yRatio - 20);
            }
        }
    }
}
