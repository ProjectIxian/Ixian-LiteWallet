using IXICore;

namespace LW.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static readonly string version = "xlwc-0.6.9"; // LiteWallet version

        // Block height at which the current version of Spixi was generated
        // Useful for optimized block header sync
        // Note: Always round last block height to 1000 and subtract 1 (i.e. if last block height is 33234, the correct value is 32999)
        public static ulong bakedBlockHeight = 1256999;

        // Block checksum (paired with bakedBlockHeight) of bakedBlockHeight
        // Useful for optimized block header sync
        public static byte[] bakedBlockChecksum = Crypto.stringToHash("490e4d45bbe16b350674c53fbe053233eb90de40f9dc1bfa146c546dac2f01dc46cd4bdba342981b39c375e4");
    }
}
