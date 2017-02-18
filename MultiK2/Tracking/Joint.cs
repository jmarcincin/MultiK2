using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Tracking
{
    public struct Joint
    {
        public Vector3 Position;
        public TrackingState PositionTrackingState;
        public Quaternion Orientation;
        public TrackingState OrientationTrackingState;
    }
}
