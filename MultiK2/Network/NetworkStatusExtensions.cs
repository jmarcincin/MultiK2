using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture.Frames;

namespace MultiK2.Network
{
    internal static class NetworkStatusExtensions
    {
        public static MediaFrameReaderStartStatus ToMediaReaderStartStatus(this OperationStatus status)
        {
            switch (status)
            {
                case OperationStatus.ResponseSuccess:
                    return MediaFrameReaderStartStatus.Success;
                case OperationStatus.ResponseFailNotAvailable:
                    return MediaFrameReaderStartStatus.DeviceNotAvailable;
                case OperationStatus.ResponseFail:
                    return MediaFrameReaderStartStatus.UnknownFailure;
                default:
                    throw new ArgumentException(nameof(status));
            }
        }
    }
}
