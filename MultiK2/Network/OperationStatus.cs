using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    internal enum OperationStatus
    {
        Request = 1,
        PushInit = 2,
        Push = 3,
        ResponseSuccess = 4,
        ResponseFail = 5,
        ResponseFailNotAvailable = 6,
    }
}
