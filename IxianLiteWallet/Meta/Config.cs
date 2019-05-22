using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DLT.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static int serverPort = 10235;
        public static int apiPort = 8001;
        public static string publicServerIP = "127.0.0.1";

        public static bool isTestNet = false; // Testnet designator

        public static int forceTimeOffset = int.MaxValue;
        // Store the device id in a cache for reuse in later instances
        public static string device_id = Guid.NewGuid().ToString();
        public static string externalIp = "";


        public static readonly string version = "xlwc-0.5.0-dev"; // LiteWallet version

    }
}
