using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    enum OperationCode
    {
        OpenReader = 0x01,
        CloseReader = 0x02,
        FrameTransfer = 0x03,
        CloseSensor = 0x04,        
    }
}
