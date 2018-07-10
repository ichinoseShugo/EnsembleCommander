using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnsembleCommander
{
    public static class Constants
    {
        public static readonly string MODE_WHOLE = "0";
        public static readonly string MODE_QUARTER = "1";
        public static readonly string MODE_ARPEGGIO = "2";
        public static readonly string MODE_DELAY = "3";
        public static readonly string MODE_FREE = "4";

        /// <summary>BluetoothWindowが閉じられているならtrue</summary>
        public static bool bwIsClosed = true;

    }
}
