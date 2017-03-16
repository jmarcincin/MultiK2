using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;

namespace MultiK2.Tracking
{
    public sealed class BodyFrame
    {
        private static Guid PoseSetBodyTracking = new Guid(0x84520b1f, 0xab61, 0x46da, 0xab, 0x1d, 0xe0, 0x13, 0x40, 0xef, 0x88, 0x4e);
        private static Guid PoseSetHandTracking = new Guid(0xf142c82c, 0x3a57, 0x4e7d, 0x81, 0x59, 0x98, 0xbd, 0xbd, 0x6c, 0xcf, 0xe2);

        internal static BodyFrame Parse(byte[] bodyBinaryData, TimeSpan? systemRelativeTime)
        {
            Body[] bodyData;
            var ms = new MemoryStream(bodyBinaryData);
            using (var reader = new BinaryReader(ms))
            {
                var customTypeGuid = new Guid(reader.ReadBytes(16));
                var entityCount = reader.ReadUInt32();

                var entityOffsets = new List<uint>();
                for (var i = 0; i < entityCount; i++)
                {
                    entityOffsets.Add(reader.ReadUInt32());
                }

                // entity data
                bodyData = new Body[entityCount];
                for (var bodyIdx = 0; bodyIdx < entityOffsets.Count; bodyIdx++)
                {
                    var entityOffset = entityOffsets[bodyIdx];
                    ms.Position = entityOffset;

                    var entityDataSize = reader.ReadUInt32();
                    var entityId = new Guid(reader.ReadBytes(16));
                    var poseSet = new Guid(reader.ReadBytes(16));
                    var poseCount = reader.ReadUInt32();
                    var isTracked = reader.ReadInt32() > 0;

                    var jointData = new Joint[poseCount];

                    // tracked poses (25)
                    // TODO check if pose count is 25
                    for (var i = 0; i < poseCount; i++)
                    {
                        var joint = new Joint();
                        joint.PositionTrackingState = (TrackingState)reader.ReadInt32();
                        joint.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        joint.OrientationTrackingState = (TrackingState)reader.ReadInt32();
                        joint.Orientation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                        jointData[i] = joint;
                    }

                    var remainingSize = entityDataSize - (ms.Position - entityOffset);
                    var customData = reader.ReadBytes((int)remainingSize);

                    if (customData.Take(32).Any(b => b > 0))
                    {
                    }

                    ms.Position -= customData.Length - 32;

                    var leftHandState = (HandState)reader.ReadInt32();
                    var leftHandStateConfidence = (TrackingConfidence)reader.ReadInt32();

                    var rightHandState = (HandState)reader.ReadInt32();
                    var rightHandStateConfidence = (TrackingConfidence)reader.ReadInt32();

                    var clip = (FrameEdges)reader.ReadUInt32();
                    ms.Position += 4;
                    var leanVector = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    var leanState = (TrackingState)reader.ReadInt32();

                    bodyData[bodyIdx] = new Body(
                        jointData,
                        entityId,
                        isTracked,
                        leftHandState,
                        leftHandStateConfidence,
                        rightHandState,
                        rightHandStateConfidence,
                        leanVector,
                        leanState,
                        clip);
                }
            }
            
            return new BodyFrame(bodyData, systemRelativeTime, bodyBinaryData);
        }

        internal static BodyFrame Parse(MediaFrameReference bodyDataFrame)
        {
            var dataBuffer = bodyDataFrame.BufferMediaFrame.Buffer;
            var binaryBodies = new byte[dataBuffer.Length];
            dataBuffer.AsStream().Read(binaryBodies, 0, binaryBodies.Length);

            return Parse(binaryBodies, bodyDataFrame.SystemRelativeTime);
        }

        public Body[] Bodies { get; }

        public TimeSpan? SystemRelativeTime { get; }

        internal byte[] BinaryData { get; }

        internal BodyFrame(Body[] bodyData, TimeSpan? relativeTime, byte[] binaryBodies)
        {
            Bodies = bodyData;
            SystemRelativeTime = relativeTime;
            BinaryData = binaryBodies;
        }
    }
}
