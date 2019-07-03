using System;

namespace DLT.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static int apiPort = 8001;

        public static bool isTestNet = false; // Testnet designator

        public static int forceTimeOffset = int.MaxValue;
        // Store the device id in a cache for reuse in later instances
        public static string device_id = Guid.NewGuid().ToString();
        public static string externalIp = "";


        public static readonly string version = "xlwc-0.5.0"; // LiteWallet version

    }
}
