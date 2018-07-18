using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnsembleCommander
{
    public static class Constants
    {
        public const int MODE_WHOLE = 0;
        public const int MODE_QUARTER = 1;
        public const int MODE_ARPEGGIO = 2;
        public const int MODE_DELAY = 3;
        public const int MODE_FREE = 4;

        /// <summary>BluetoothWindowが閉じられているならtrue</summary>
        public static bool bwIsClosed = true;
    }
}
