using MultiK2.Network;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Networking;

namespace MultiK2
{
    public sealed class Sensor
    {
        private static string PoseTrackingSubType = new Guid(0x69232056, 0x2ed9, 0x4d0e, 0x89, 0xcc, 0x5d, 0x27, 0x34, 0xa5, 0x68, 0x8).ToString("B").ToUpper();
        
        private MediaFrameSourceGroup _sourceGroup;
        private MediaCapture _mediaCapture;

        private ColorFrameReader _colorReader;
        private DepthFrameReader _depthReader;
        private BodyIndexFrameReader _bodyIndexReader;
        private BodyFrameReader _bodyReader;
        private AudioFrameReader _audioReader;

        private CoordinateMapper _coordinateMapper = new CoordinateMapper();

        private NetworkServer _networkServer;
        private NetworkClient _networkClient;

        public static IAsyncOperation<Sensor> GetDefaultAsync()
        {
            return Task.Run(async () =>
            {
                // todo: remove custom from required streams - Xbox doesn't expose it yet
                var cameraSensorGroups = await MediaFrameSourceGroup.FindAllAsync();
                var sourceGroup = cameraSensorGroups.FirstOrDefault(
                    group =>
                    group.SourceInfos.Any(si => si.SourceKind == MediaFrameSourceKind.Color) &&
                    group.SourceInfos.Any(si => si.SourceKind == MediaFrameSourceKind.Depth) &&
                    group.SourceInfos.Any(si => si.SourceKind == MediaFrameSourceKind.Infrared) &&
                    group.SourceInfos.Any(si => si.SourceKind == MediaFrameSourceKind.Custom));

                return sourceGroup == null ? null : new Sensor(sourceGroup);
            }).AsAsyncOperation();
        }

        public static Sensor CreateNetworkSensor(string ipAddress, int port)
        {
            return new Sensor(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        }
                
        private Sensor(IPEndPoint remoteEndPoint)
        {
            _networkClient = new NetworkClient(remoteEndPoint);
            Type = SensorType.NetworkClient;
        }

        private Sensor(MediaFrameSourceGroup kinectMediaGroup)
        {
            _sourceGroup = kinectMediaGroup;
        }

        public bool IsActive => _mediaCapture != null || _networkClient != null && _networkClient.IsConnected;

        public SensorType Type { get; private set; }

        public bool AllowRemoteClient { get; set; }

        // todo range check (ushort)
        public int ServerPort { get; set; } = 8599;

        public IAsyncAction OpenAsync()
        {
            return Task.Run(async () =>
            {
                if (Type != SensorType.NetworkClient)
                {
                    Type = AllowRemoteClient ? SensorType.NetworkServer : SensorType.Local;
                    if (_mediaCapture == null)
                    {
                        var captureSettings = new MediaCaptureInitializationSettings
                        {
                            SourceGroup = _sourceGroup,
                            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                            StreamingCaptureMode = StreamingCaptureMode.Video,
                            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                        };
                        _mediaCapture = new MediaCapture();
                        await _mediaCapture.InitializeAsync(captureSettings);
                    }
                    if (Type == SensorType.NetworkServer && _networkServer == null)
                    {
                        _networkServer = new NetworkServer(this);
                        await _networkServer.StartListener();
                    }
                }
                else
                {
                    await _networkClient.OpenNetworkAsync();
                }              
            }).AsAsyncAction();
        }

        public IAsyncAction CloseAsync()
        {
            return Task.Run(async () =>
            {
                await _bodyIndexReader?.CloseAsync();
                _bodyIndexReader?.Dispose();
                _bodyIndexReader = null;

                await _bodyReader?.CloseAsync();
                _bodyReader?.Dispose();
                _bodyReader = null;

                await _colorReader?.CloseAsync();
                _colorReader?.Dispose();
                _colorReader = null;

                await _depthReader?.CloseAsync();
                _depthReader?.Dispose();
                _depthReader = null;

                _audioReader?.Close();
                _audioReader?.Dispose();
                _audioReader = null;

                _mediaCapture?.Dispose();
                _mediaCapture = null;
                
                _networkClient = null;
                _networkServer = null;                
            }).AsAsyncAction();
        }

        public CoordinateMapper GetCoordinateMapper()
        {
            return _coordinateMapper;
        }

        public IAsyncOperation<ColorFrameReader> OpenColorFrameReaderAsync(ReaderConfig config = ReaderConfig.Default)
        {
            return Task.Run(async () =>
            {
                if (_colorReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        _colorReader = new ColorFrameReader(this, _networkClient);
                    }
                    else
                    {
                        var colorSourceInfo = _sourceGroup.SourceInfos.FirstOrDefault(si => si.SourceKind == MediaFrameSourceKind.Color);
                        if (colorSourceInfo != null)
                        {
                            MediaFrameSource colorSource;
                            if (_mediaCapture.FrameSources.TryGetValue(colorSourceInfo.Id, out colorSource))
                            {
                                var colorMediaReader = await _mediaCapture.CreateFrameReaderAsync(colorSource);
                                _colorReader = new ColorFrameReader(this, colorMediaReader, config);
                            }
                        }
                    }
                }
                await _colorReader?.OpenAsync();
                return _colorReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<DepthFrameReader> OpenDepthFrameReaderAsync()
        {
            return Task.Run(async () =>
            {
                if (_depthReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        _depthReader = new DepthFrameReader(this, _networkClient);
                    }
                    else
                    {
                        var depthSourceInfo = _sourceGroup.SourceInfos.FirstOrDefault(si => si.SourceKind == MediaFrameSourceKind.Depth);
                        if (depthSourceInfo != null)
                        {
                            MediaFrameSource depthSource;
                            if (_mediaCapture.FrameSources.TryGetValue(depthSourceInfo.Id, out depthSource))
                            {
                                var depthMediaReader = await _mediaCapture.CreateFrameReaderAsync(depthSource);
                                _depthReader = new DepthFrameReader(this, depthMediaReader);
                            }
                        }
                    }
                }
                await _depthReader?.OpenAsync();
                return _depthReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BodyIndexFrameReader> OpenBodyIndexFrameReaderAsync()
        {
            return Task.Run(async () =>
            {
                if (_bodyIndexReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        _bodyIndexReader = new BodyIndexFrameReader(this, _networkClient);
                    }
                    else
                    {
                        var bodyIndexSourceInfo = _sourceGroup.SourceInfos.Where(si => si.SourceKind == MediaFrameSourceKind.Custom);
                        foreach (var sourceInfo in bodyIndexSourceInfo)
                        {
                            MediaFrameSource customSource;
                            if (_mediaCapture.FrameSources.TryGetValue(sourceInfo.Id, out customSource) &&
                            customSource.CurrentFormat.MajorType == "Video" &&
                            customSource.CurrentFormat.Subtype == "L8")
                            {
                                var bodyIndexMediaReader = await _mediaCapture.CreateFrameReaderAsync(customSource);
                                _bodyIndexReader = new BodyIndexFrameReader(this, bodyIndexMediaReader);
                                break;
                            }
                        }
                    }
                }
                await _bodyIndexReader?.OpenAsync();
                return _bodyIndexReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BodyFrameReader> OpenBodyFrameReaderAsync()
        {   
            return Task.Run(async () =>
            {
                if (_bodyReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        _bodyReader = new BodyFrameReader(this, _networkClient);
                    }
                    else
                    {
                        var bodyIndexSourceInfo = _sourceGroup.SourceInfos.Where(si => si.SourceKind == MediaFrameSourceKind.Custom);
                        foreach (var sourceInfo in bodyIndexSourceInfo)
                        {
                            MediaFrameSource customSource;
                            if (_mediaCapture.FrameSources.TryGetValue(sourceInfo.Id, out customSource) &&
                            customSource.CurrentFormat.MajorType == "Perception" &&
                            customSource.CurrentFormat.Subtype == PoseTrackingSubType)
                            {
                                var bodyMediaReader = await _mediaCapture.CreateFrameReaderAsync(customSource);
                                _bodyReader = new BodyFrameReader(this, bodyMediaReader);
                                break;
                            }
                        }
                    }
                }
                await _bodyReader?.OpenAsync();
                return _bodyReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<AudioFrameReader> OpenAudioFrameReaderAsync()
        {   
            return Task.Run(async () =>
            {
                if (_audioReader == null)
                {   
                    var microphones = await DeviceInformation.FindAllAsync(DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.AudioCapture));
                    var kinectMicArray = microphones.FirstOrDefault(mic => mic.Name.ToLowerInvariant().Contains("xbox nui sensor"));

                    if (kinectMicArray != null)
                    {
                        //TODO: review parameters
                        var settings = new AudioGraphSettings(AudioRenderCategory.Speech);
                        settings.EncodingProperties = AudioEncodingProperties.CreatePcm(16000, 4, 32);
                        settings.EncodingProperties.Subtype = MediaEncodingSubtypes.Float;
                        settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
                        settings.DesiredRenderDeviceAudioProcessing = Windows.Media.AudioProcessing.Raw;

                        var audioGraphResult = await AudioGraph.CreateAsync(settings);
                        if (audioGraphResult.Status == AudioGraphCreationStatus.Success)
                        {
                            var inputNodeResult = await audioGraphResult.Graph.CreateDeviceInputNodeAsync(MediaCategory.Speech, audioGraphResult.Graph.EncodingProperties, kinectMicArray);

                            if (inputNodeResult.Status == AudioDeviceNodeCreationStatus.Success)
                            {
                                var output = audioGraphResult.Graph.CreateFrameOutputNode(audioGraphResult.Graph.EncodingProperties);
                                _audioReader = new AudioFrameReader(audioGraphResult.Graph, output);
                            }
                        }
                    }
                }
                _audioReader?.Open();
                return _audioReader;
            }).AsAsyncOperation();
        }
    }    
}
