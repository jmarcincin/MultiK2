using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    internal enum OperationCode
    {
        OpenReader = 1,
        CloseReader = 2,
        CloseSensor = 3,

        BodyFrameTransfer = 16,
        DepthFrameTransfer = 17,
        ColorFrameTransfer = 18,
        BodyIndexFrameTransfer = 19,
        AudioFrameTransfer = 20,
        InfraredFrameTransfer = 21,
        Infrared2FrameTrasfer = 22,
        UserFrameTransfer = 23,
    }
}
