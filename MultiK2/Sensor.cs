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

namespace MultiK2
{
    public sealed class Sensor
    {
        private static string PoseTrackingSubType = new Guid(0x69232056, 0x2ed9, 0x4d0e, 0x89, 0xcc, 0x5d, 0x27, 0x34, 0xa5, 0x68, 0x8).ToString("B").ToUpper();
        
        private readonly MediaFrameSourceGroup _sourceGroup;
        private readonly CoordinateMapper _coordinateMapper = new CoordinateMapper();

        private MediaCapture _mediaCapture;
        private NetworkServer _networkServer;
        private NetworkClient _networkClient;
        
        internal ColorFrameReader ColorReader { get; private set; }
        internal DepthFrameReader DepthReader { get; private set; }
        internal BodyIndexFrameReader BodyIndexReader { get; private set; }
        internal BodyFrameReader BodyReader { get; private set; }
        internal AudioFrameReader AudioReader { get; private set; }

        public bool IsActive => _mediaCapture != null || _networkClient != null && _networkClient.IsConnected;

        public SensorType Type { get; private set; }

        public bool AllowRemoteClient { get; set; }

        // todo range check (ushort)
        public int ServerPort { get; set; } = 8599;

        public event EventHandler<byte[]> UserDefinedDataReceived;

        private Sensor(IPEndPoint remoteEndPoint)
        {
            _networkClient = new NetworkClient(remoteEndPoint);
            Type = SensorType.NetworkClient;
        }

        private Sensor(MediaFrameSourceGroup kinectMediaGroup)
        {
            _sourceGroup = kinectMediaGroup;
        }

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
                        _networkServer.ConnectionEstablished += NetworkConnectionEstablished;
                        _networkServer.ConnectionClosed += NetworkConnectionClosed;
                        _networkServer.CustomDataReceived += NetworkCustomDataReceived;
                        _networkServer.StartListener();
                        
                    }
                }
                else
                {
                    _networkClient.ConnectionEstablished += NetworkConnectionEstablished;
                    _networkClient.ConnectionClosed += NetworkConnectionClosed;
                    _networkClient.CustomDataReceived += NetworkCustomDataReceived;
                    await _networkClient.OpenNetworkAsync();
                }              
            }).AsAsyncAction();
        }
        
        public IAsyncAction CloseAsync()
        {
            return Task.Run(async () =>
            {
                await BodyIndexReader?.CloseAsync();
                BodyIndexReader?.Dispose();
                BodyIndexReader = null;

                await BodyReader?.CloseAsync();
                BodyReader?.Dispose();
                BodyReader = null;

                await ColorReader?.CloseAsync();
                ColorReader?.Dispose();
                ColorReader = null;

                await DepthReader?.CloseAsync();
                DepthReader?.Dispose();
                DepthReader = null;

                AudioReader?.Close();
                AudioReader?.Dispose();
                AudioReader = null;

                _mediaCapture?.Dispose();
                _mediaCapture = null;

                _networkClient?.CloseConnection();
                _networkClient = null;

                _networkServer?.CloseConnection();
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
                if (ColorReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        ColorReader = new ColorFrameReader(this, _networkClient, config);
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
                                ColorReader = new ColorFrameReader(this, colorMediaReader, config);
                            }
                        }
                    }
                }
                await ColorReader?.OpenAsync();
                return ColorReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<DepthFrameReader> OpenDepthFrameReaderAsync()
        {
            return Task.Run(async () =>
            {
                if (DepthReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        DepthReader = new DepthFrameReader(this, _networkClient);
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
                                DepthReader = new DepthFrameReader(this, depthMediaReader);
                            }
                        }
                    }
                }
                await DepthReader?.OpenAsync();
                return DepthReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BodyIndexFrameReader> OpenBodyIndexFrameReaderAsync()
        {
            return Task.Run(async () =>
            {
                if (BodyIndexReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        BodyIndexReader = new BodyIndexFrameReader(this, _networkClient);
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
                                BodyIndexReader = new BodyIndexFrameReader(this, bodyIndexMediaReader);
                                break;
                            }
                        }
                    }
                }
                await BodyIndexReader?.OpenAsync();
                return BodyIndexReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BodyFrameReader> OpenBodyFrameReaderAsync()
        {   
            return Task.Run(async () =>
            {
                if (BodyReader == null)
                {
                    if (Type == SensorType.NetworkClient)
                    {
                        BodyReader = new BodyFrameReader(this, _networkClient);
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
                                BodyReader = new BodyFrameReader(this, bodyMediaReader);
                                break;
                            }
                        }
                    }
                }
                await BodyReader?.OpenAsync();
                return BodyReader;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<AudioFrameReader> OpenAudioFrameReaderAsync()
        {   
            return Task.Run(async () =>
            {
                if (AudioReader == null)
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
                                AudioReader = new AudioFrameReader(audioGraphResult.Graph, output);
                            }
                        }
                    }
                }
                AudioReader?.Open();
                return AudioReader;
            }).AsAsyncOperation();
        }

        public void SendUserDefinedData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_networkClient != null)
            {
                _networkClient.SendCustomFrameData(data);
            }
            else if (_networkServer != null)
            {
                _networkServer.SendCustomFrameData(data);
            }
            else
            {
                throw new InvalidOperationException("Operation is supported only with network sensor in role of server or client");
            }
        }

        private void NetworkCustomDataReceived(object sender, byte[] e)
        {
            UserDefinedDataReceived?.Invoke(this, e);
        }

        private void NetworkConnectionClosed(object sender, bool e)
        {
            // TODO: handle also mediacapture state change?
        }

        private void NetworkConnectionEstablished(object sender, IPEndPoint e)
        {
        }
    }    
}
