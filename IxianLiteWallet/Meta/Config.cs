using Fclp;
using IxianLiteWallet;
using System;

namespace LW.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static readonly string version = "xlwc-0.9.0b"; // LiteWallet version

        private static string outputHelp()
        {
            Program.noStart = true;

            Console.WriteLine("Starts a new instance of IxianLiteWallet");
            Console.WriteLine("");
            Console.WriteLine(" IxianLiteWallet.exe [-h] [-v] [-w ixian.wal]");
            Console.WriteLine("");
            Console.WriteLine("    -h\t\t\t Displays this help");
            Console.WriteLine("    -v\t\t\t Displays version");
            Console.WriteLine("    -w\t\t\t Specify name of the wallet file");

            return "";
        }
        private static string outputVersion()
        {
            Program.noStart = true;

            // Do nothing since version is the first thing displayed

            return "";
        }
        public static void init(string[] args)
        {
            var cmd_parser = new FluentCommandLineParser();
            
            cmd_parser.SetupHelp("h", "help").Callback(text => outputHelp());
            cmd_parser.Setup<bool>('v', "version").Callback(text => outputVersion());
            cmd_parser.Setup<string>('w', "wallet").Callback(value => walletFile = value).Required();

            cmd_parser.Parse(args);


            if (Program.noStart)
            {
                return;
            }
        }
    }
}
