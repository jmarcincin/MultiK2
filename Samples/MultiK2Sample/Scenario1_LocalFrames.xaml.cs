using MultiK2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace MultiK2Sample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Scenario1_LocalFrames : Page
    {
        private Sensor _kinectSensor;
        private ColorFrameReader _colorReader;
        private DepthFrameReader _depthReader;
        private BodyIndexFrameReader _bodyIndexReader;
        private BodyFrameReader _bodyReader;

        private CameraIntrinsics _colorCameraIntrinsics;
        private CameraIntrinsics _depthCameraIntrinsics;

        private SoftwareBitmap _colorBackBuffer;
        private SoftwareBitmap _depthBackBuffer;
        private SoftwareBitmap _bodyIndexBackBuffer;

        private int _isRenderingColor = 0;
        private int _isRenderingDepth = 0;
        private int _isRederingBodyIndex = 0;

        public Scenario1_LocalFrames()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Application.Current.Resuming += Application_Resuming;
            Application.Current.EnteredBackground += Application_EnteredBackground;
            Application.Current.LeavingBackground += Application_LeavingBackground;
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            Application.Current.Resuming -= Application_Resuming;
            Application.Current.EnteredBackground -= Application_EnteredBackground;
            Application.Current.LeavingBackground -= Application_LeavingBackground;

            await _kinectSensor?.CloseAsync();
        }

        private async void Application_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            await InitializeKinect();
            deferral.Complete();
        }

        private async void Application_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            await _kinectSensor?.CloseAsync();
            deferral.Complete();
        }

        private async void Application_Resuming(object sender, object e)
        {
            await InitializeKinect();
        }

        private async Task InitializeKinect()
        {
            _kinectSensor = await Sensor.GetDefaultAsync();
            if (_kinectSensor != null)
            {
                await _kinectSensor.OpenAsync();
                
                _colorReader = await _kinectSensor.OpenColorFrameReaderAsync();
                _depthReader = await _kinectSensor.OpenDepthFrameReaderAsync();
                _bodyIndexReader = await _kinectSensor.OpenBodyIndexFrameReaderAsync();
                _bodyReader = await _kinectSensor.OpenBodyFrameReaderAsync();

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

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            ColorSkeletonOutput.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    var coordinateMapper = _kinectSensor.GetCoordinateMapper();
                    ColorSkeletonOutput.FillBodies(
                        e.BodyFrame.Bodies,
                        _colorCameraIntrinsics,
                        p => coordinateMapper.MapDepthSpacePointToColor(p));

                    DepthSkeletonOutput.FillBodies(
                        e.BodyFrame.Bodies,
                        _depthCameraIntrinsics,
                        p => p);

                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void BodyIndexReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
        {
            var bitmap = e.GetDisplayableBitmap();
            bitmap = Interlocked.Exchange(ref _bodyIndexBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            BodyIndexOutput.Dispatcher.RunAsync(
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
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            _depthCameraIntrinsics = e.CameraIntrinsics;

            var bitmap = e.GetDisplayableBitmap();
            bitmap = Interlocked.Exchange(ref _depthBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            DepthOutput.Dispatcher.RunAsync(
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
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            _colorCameraIntrinsics = e.CameraIntrinsics;
            var bitmap = e.GetDisplayableBitmap(BitmapPixelFormat.Bgra8);
            bitmap = Interlocked.Exchange(ref _colorBackBuffer, bitmap);
            bitmap?.Dispose();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            ColorOutput.Dispatcher.RunAsync(
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
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
