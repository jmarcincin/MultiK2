using System;
using Windows.Media.Capture.Frames;

using MultiK2.Tracking;
using Windows.Foundation;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media;

namespace MultiK2
{
    public sealed class AudioFrameReader
    {
        private AudioGraph _audioGraph;
        private AudioFrameOutputNode _outputNode;
        private bool _isStarted;

        public event EventHandler<AudioFrameArrivedEventArgs> FrameArrived;
        
        internal AudioFrameReader(AudioGraph audioGraph, AudioFrameOutputNode node)
        {
            _audioGraph = audioGraph;
            _outputNode = node;
            _audioGraph.QuantumProcessed += AudioGraph_QuantumProcessed;
        }

        private unsafe void AudioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            var subscribers = FrameArrived;

            if (subscribers != null)
            {
                float[] audioData;
                var frame = _outputNode.GetFrame();

                using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
                using (var bufferReference = buffer.CreateReference())
                {
                    byte* sourcePtr;
                    uint sourceCapacity;

                    ((IMemoryBufferByteAccess)bufferReference).GetBuffer(out sourcePtr, out sourceCapacity);
                    
                    // PCM 32bit for 4 channels interleaved
                    if (sourceCapacity % 4 != 0)
                    {
                        throw new DataMisalignedException();
                    }

                    audioData = new float[sourceCapacity / 4];

                    float* floatSourcePtr = (float*)sourcePtr;

                    // primitive memcpy
                    fixed (float* floatTargetPtr = audioData)
                    {
                        for (var i = 0; i < sourceCapacity / 4; i++)
                        {
                            floatTargetPtr[i] = floatSourcePtr[i];
                        }
                    }
                }

                var audioArgs = new AudioFrameArrivedEventArgs(this, frame.RelativeTime.Value, frame.Duration.Value, audioData);
                subscribers(this, audioArgs);
            }
        }

        /*
        public AudioFrameInputNode CreateAudioInputNode(AudioGraph audioGraph)
        {
            var inputNode = audioGraph.CreateFrameInputNode(AudioEncodingProperties.CreatePcm(16000, 4, 32));
            inputNode.QuantumStarted += InputNode_QuantumStarted;
        }

        private void InputNode_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            throw new NotImplementedException();
        }*/

        public void Open()
        {
            if (!_isStarted)
            {
                _audioGraph.Start();
                _isStarted = true;
            }
        }

        public void Close()
        {
            _audioGraph.Stop();
        }

        internal void Dispose()
        {
            _audioGraph?.Dispose();
            _audioGraph = null;
        }
    }
    
    public sealed class AudioFrameArrivedEventArgs
    {
        public object Source { get; }

        public float[] AudioFrame { get; }

        public TimeSpan Duration { get; }

        public TimeSpan RelativeTime { get; }
        
        internal AudioFrameArrivedEventArgs(object source, TimeSpan relativeTime, TimeSpan duration, float[] audioFrame)
        {
            // TODO: expose channels as separate arrays or in interleaved form? / helper methods in args?
            Source = source;
            RelativeTime = relativeTime;
            AudioFrame = audioFrame;
            Duration = duration;
        }        
    }
}
