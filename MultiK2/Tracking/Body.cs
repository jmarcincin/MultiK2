using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Tracking
{
    public sealed class Body
    {
        public IReadOnlyDictionary<JointType, Joint> Joints { get; }

        public bool IsTracked { get; }

        public Guid EntityId { get; }

        public HandState HandStateLeft { get; }
        
        public TrackingConfidence ConfidenceLeft { get; }

        public HandState HandStateRight { get; }

        public TrackingConfidence ConfidenceRight { get; }

        public Vector2 Lean { get; }

        public TrackingState LeanTrackingState { get; }

        public FrameEdges ClippedEdges { get; }

        internal Body(
            Joint[] jointData, 
            Guid entityId,
            bool isTracked,
            HandState leftHandState,
            TrackingConfidence leftHandConfidence,
            HandState rightHandState,
            TrackingConfidence rightHandConfidence,
            Vector2 lean,
            TrackingState leanState,
            FrameEdges clippedEdges)
        {
            Joints = new EnumDictionary<Joint>(jointData);
            IsTracked = isTracked;
            EntityId = entityId;
            HandStateLeft = leftHandState;
            ConfidenceLeft = leftHandConfidence;
            HandStateRight = rightHandState;
            ConfidenceRight = rightHandConfidence;
            Lean = lean;
            LeanTrackingState = leanState;
            ClippedEdges = clippedEdges;
        }

        public IEnumerable<Bone> CreateSkeleton()
        {
            yield return new Bone(Joints[JointType.Head], Joints[JointType.Neck]);
            yield return new Bone(Joints[JointType.Neck], Joints[JointType.SpineShoulder]);
            yield return new Bone(Joints[JointType.SpineShoulder], Joints[JointType.ShoulderRight]);
            yield return new Bone(Joints[JointType.SpineShoulder], Joints[JointType.ShoulderLeft]);
            yield return new Bone(Joints[JointType.ShoulderRight], Joints[JointType.ElbowRight]);
            yield return new Bone(Joints[JointType.ShoulderLeft], Joints[JointType.ElbowLeft]);
            yield return new Bone(Joints[JointType.ElbowRight], Joints[JointType.WristRight]);
            yield return new Bone(Joints[JointType.ElbowLeft], Joints[JointType.WristLeft]);
            yield return new Bone(Joints[JointType.WristRight], Joints[JointType.HandRight]);
            yield return new Bone(Joints[JointType.WristLeft], Joints[JointType.HandLeft]);
            yield return new Bone(Joints[JointType.HandRight], Joints[JointType.HandTipRight]);
            yield return new Bone(Joints[JointType.HandLeft], Joints[JointType.HandTipLeft]);
            yield return new Bone(Joints[JointType.HandRight], Joints[JointType.ThumbRight]);
            yield return new Bone(Joints[JointType.HandLeft], Joints[JointType.ThumbLeft]);
            yield return new Bone(Joints[JointType.SpineShoulder], Joints[JointType.SpineMid]);
            yield return new Bone(Joints[JointType.SpineMid], Joints[JointType.SpineBase]);
            yield return new Bone(Joints[JointType.SpineBase], Joints[JointType.HipRight]);
            yield return new Bone(Joints[JointType.SpineBase], Joints[JointType.HipLeft]);
            yield return new Bone(Joints[JointType.HipRight], Joints[JointType.KneeRight]);
            yield return new Bone(Joints[JointType.HipLeft], Joints[JointType.KneeLeft]);
            yield return new Bone(Joints[JointType.KneeRight], Joints[JointType.AnkleRight]);
            yield return new Bone(Joints[JointType.KneeLeft], Joints[JointType.AnkleLeft]);
            yield return new Bone(Joints[JointType.AnkleRight], Joints[JointType.FootRight]);
            yield return new Bone(Joints[JointType.AnkleLeft], Joints[JointType.FootLeft]);
        }
    }
}
