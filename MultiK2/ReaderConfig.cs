using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiK2
{
    [Flags]
    public enum ReaderConfig
    {
        Default = 0,
        HalfRate = 0x01,
        HalfResolution = 0x02,
        OnlyMetadata = 0x04
    }
}
