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

        /// <summary>
        /// Downsize horizontal & vertical resolution to half. Supported only by Color Frame reader.
        /// </summary>
        HalfResolution = 0x01,

        /// <summary>
        /// NOT SUPPORTED. For future use.
        /// </summary>
        HalfRate = 0x02
    }
}
