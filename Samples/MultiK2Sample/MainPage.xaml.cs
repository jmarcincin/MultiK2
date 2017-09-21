using MultiK2;
using MultiK2.Tracking;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace MultiK2Sample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Sensor _clientSensor;

        Sensor _kinectSensor;
        ColorFrameReader _colorReader;
        DepthFrameReader _depthReader;
        BodyIndexFrameReader _bodyIndexReader;
        BodyFrameReader _bodyReader;

        CameraIntrinsics _colorCameraIntrinsics;
        CameraIntrinsics _depthCameraIntrinsics;

        SoftwareBitmap _colorBackBuffer;
        SoftwareBitmap _depthBackBuffer;
        SoftwareBitmap _bodyIndexBackBuffer;
        
        int _isRenderingColor = 0;
        int _isRenderingDepth = 0;
        int _isRederingBodyIndex = 0;

        Body[] _bodies;

        public MainPage()
        {
            this.InitializeComponent();
            
            // TODO: state transition locking > e.g. EnteredBackground + Suspending
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;
            Application.Current.EnteredBackground += Application_EnteredBackground;
            Application.Current.LeavingBackground += Application_LeavingBackground;

            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private async void CompositionTarget_Rendering(object sender, object e)
        {
            if (Interlocked.CompareExchange(ref _isRenderingDepth, 1, 0) == 0)
            {
                try
                {
                    SoftwareBitmap availableFrame = null;
                    while ((availableFrame = Interlocked.Exchange(ref _depthBackBuffer, null)) != null)
                    {
                        await((SoftwareBitmapSource)DepthOutput.Source).SetBitmapAsync(availableFrame);
                        availableFrame.Dispose();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isRenderingDepth, 0);
                }
            }

            if (Interlocked.CompareExchange(ref _isRenderingColor, 1, 0) == 0)
            {
                try
                {
                    SoftwareBitmap availableFrame = null;
                    while ((availableFrame = Interlocked.Exchange(ref _colorBackBuffer, null)) != null)
                    {
                        await ((SoftwareBitmapSource)ColorOutput.Source).SetBitmapAsync(availableFrame);
                        availableFrame.Dispose();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isRenderingColor, 0);
                }
            }

            if (Interlocked.CompareExchange(ref _isRederingBodyIndex, 1, 0) == 0)
            {
                try
                {
                    SoftwareBitmap availableFrame = null;
                    while ((availableFrame = Interlocked.Exchange(ref _bodyIndexBackBuffer, null)) != null)
                    {
                        await ((SoftwareBitmapSource)BodyIndexOutput.Source).SetBitmapAsync(availableFrame);
                        availableFrame.Dispose();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isRederingBodyIndex, 0);
                }
            }

            if (_bodies != null)
            {
                var coordinateMapper = _kinectSensor.GetCoordinateMapper();
                SkeletonOutput.FillBodies(
                    _bodies,
                    _colorCameraIntrinsics,
                    p => coordinateMapper.MapDepthSpacePointToColor(p));
                
                DepthSkeletonOutput.FillBodies(
                    _bodies,
                    _depthCameraIntrinsics,
                    p => p);
            }
        }

        private async void Application_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            await InitializeKinect();
            deferral.Complete();
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private async void Application_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            var deferral = e.GetDeferral();
            await _kinectSensor?.CloseAsync();
            deferral.Complete();
        }
        
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            //await InitializeKinect();
        }

        private async void Application_Resuming(object sender, object e)
        {
            await InitializeKinect();
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private async void Application_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //await _kinectSensor?.CloseAsync();           
            deferral.Complete();
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }
        
        private async Task InitializeKinect()
        {
            _kinectSensor = await Sensor.GetDefaultAsync();
            if (_kinectSensor != null)
            {
                //_kinectSensor.AllowRemoteClient = true;
                await _kinectSensor.OpenAsync();
                
                _colorReader = await _kinectSensor.OpenColorFrameReaderAsync(ReaderConfig.HalfResolution);
                _depthReader = await _kinectSensor.OpenDepthFrameReaderAsync();
                /*_bodyIndexReader = await _kinectSensor.OpenBodyIndexFrameReaderAsync();
                _bodyReader = await _kinectSensor.OpenBodyFrameReaderAsync();
                */

                // network connection testing
                //_clientSensor = Sensor.CreateNetworkSensor("127.0.0.1", 8599);
                //_clientSensor = Sensor.CreateNetworkSensor("10.122.1.2", 8599);
                //await _clientSensor.OpenAsync();

                //_colorReader = await _clientSensor.OpenColorFrameReaderAsync(ReaderConfig.HalfResolution);
                //_depthReader = await _clientSensor.OpenDepthFrameReaderAsync();
                //_bodyIndexReader = await _clientSensor.OpenBodyIndexFrameReaderAsync();
                //_bodyReader = await _clientSensor.OpenBodyFrameReaderAsync();
                
                if (_depthReader != null)
                {
                    DepthOutput.Source = new SoftwareBitmapSource();
                    _depthReader.FrameArrived += DepthReader_FrameArrived;
                }

                if (_colorReader != null)
                {
                    ColorOutput.Source = new SoftwareBitmapSource();
                    _colorReader.FrameArrived += ColorReader_FrameArrived;
                }

                if (_bodyReader != null)
                {
                    _bodyReader.FrameArrived += BodyReader_FrameArrived;
                }

                if (_bodyIndexReader != null)
                {
                    BodyIndexOutput.Source = new SoftwareBitmapSource();
                    _bodyIndexReader.FrameArrived += BodyIndexReader_FrameArrived;
                }                
            }
        }
        
        private void AudioReader_FrameArrived(object sender, AudioFrameArrivedEventArgs e)
        {
            if (e.Duration > TimeSpan.FromMilliseconds(1))
            {
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            _bodies = e.BodyFrame.Bodies;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            /*SkeletonOutput.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    var coordinateMapper = _kinectSensor.GetCoordinateMapper();
                    SkeletonOutput.FillBodies(
                        e.BodyFrame.Bodies,
                        _colorCameraIntrinsics,
                        p => coordinateMapper.MapDepthSpacePointToColor(p));

                    DepthSkeletonOutput.FillBodies(
                        e.BodyFrame.Bodies,
                        _depthCameraIntrinsics,
                        p => p);
                    
                });*/
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void BodyIndexReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
        {
            var bitmap = e.GetDisplayableBitmap();
            bitmap = Interlocked.Exchange(ref _bodyIndexBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            /*BodyIndexOutput.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    if (Interlocked.CompareExchange(ref _isRederingBodyIndex, 1, 0) == 0)
                    {
                        try
                        {
                            SoftwareBitmap availableFrame = null;
                            while ((availableFrame = Interlocked.Exchange(ref _bodyIndexBackBuffer, null)) != null)
                            {
                                await ((SoftwareBitmapSource)BodyIndexOutput.Source).SetBitmapAsync(availableFrame);
                                availableFrame.Dispose();
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isRederingBodyIndex, 0);
                        }
                    }
                });*/
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
        
        private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            _depthCameraIntrinsics = e.CameraIntrinsics;

            var bitmap = e.GetDisplayableBitmap();
            bitmap = Interlocked.Exchange(ref _depthBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            /*DepthOutput.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    if (Interlocked.CompareExchange(ref _isRenderingDepth, 1, 0) == 0)
                    {
                        try
                        {
                            SoftwareBitmap availableFrame = null;
                            while ((availableFrame = Interlocked.Exchange(ref _depthBackBuffer, null)) != null)
                            {
                                await ((SoftwareBitmapSource)DepthOutput.Source).SetBitmapAsync(availableFrame);
                                availableFrame.Dispose();
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isRenderingDepth, 0);
                        }
                    }
                });*/
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            _colorCameraIntrinsics = e.CameraIntrinsics;
            var bitmap = e.GetDisplayableBitmap(BitmapPixelFormat.Bgra8);
            bitmap = Interlocked.Exchange(ref _colorBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            /*ColorOutput.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    if (Interlocked.CompareExchange(ref _isRenderingColor, 1, 0) == 0)
                    {
                        try
                        {
                            SoftwareBitmap availableFrame = null;
                            while ((availableFrame = Interlocked.Exchange(ref _colorBackBuffer, null)) != null)
                            {
                                await ((SoftwareBitmapSource)ColorOutput.Source).SetBitmapAsync(availableFrame);
                                availableFrame.Dispose();
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isRenderingColor, 0);
                        }
                    }
                });*/
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
