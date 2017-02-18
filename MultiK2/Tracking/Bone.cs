using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Tracking
{
    public sealed class Bone
    {
        public Joint Joint1 { get; }

        public Joint Joint2 { get; }

        public TrackingState TrackingState
        {
            get
            {
                if (Joint1.PositionTrackingState == TrackingState.NotTracked || Joint2.PositionTrackingState == TrackingState.NotTracked)
                {
                    return TrackingState.NotTracked;
                }
                if (Joint1.PositionTrackingState == TrackingState.Tracked && Joint2.PositionTrackingState == TrackingState.Tracked)
                {
                    return TrackingState.Tracked;
                }
                return TrackingState.Inferred;
            }
        }

        internal Bone(Joint joint1, Joint joint2)
        {
            Joint1 = joint1;
            Joint2 = joint2;
        }
    }
}
