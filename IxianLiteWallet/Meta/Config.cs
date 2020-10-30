using IXICore;

namespace LW.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static readonly string version = "xlwc-0.7.5"; // LiteWallet version

        // Block height at which the current version of Spixi was generated
        // Useful for optimized block header sync
        // Note: Always round last block height to 1000 and subtract 1 (i.e. if last block height is 33234, the correct value is 32999)
        public static ulong bakedBlockHeight = 1499999;

        // Block checksum (paired with bakedBlockHeight) of bakedBlockHeight
        // Useful for optimized block header sync
        public static byte[] bakedBlockChecksum = Crypto.stringToHash("fde5ee7d5ca2744a80f38f4db916f1ba66d5626dd00206d01fb47daf7f61140c443328942f201dcefc883f7f");
    }
}
