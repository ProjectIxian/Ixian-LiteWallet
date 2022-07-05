using IXICore;
using IXICore.Meta;
using LW.Meta;
using System;

namespace IxianLiteWallet
{
    class Program
    {
        public static bool noStart = false;

        public static bool running = false;

        private static Node node = null;

        public static Commands commands = null;

        static void Main(string[] args)
        {
            if(!onStart(args))
            {
                return;
            }

            mainLoop();
            onStop();
        }

        static bool onStart(string[] args)
        {
            running = true;

            Console.WriteLine("Ixian Lite Wallet {0} ({1})", Config.version, CoreConfig.version);

            // Read configuration from command line
            Config.init(args);

            if (noStart)
            {
                return false;
            }

            commands = new Commands();

            // Initialize the node
            node = new Node();

            // Start the node
            node.start();

            return true;
        }

        static void mainLoop()
        {
            LineEditor le = new LineEditor("IxianLiteWallet");
            Console.WriteLine("Type help to see a list of available commands.\n");

            while (running && !IxianHandler.forceShutdown)
            {
                string line = le.Edit("IxianLiteWallet> ", "");
                commands.handleCommand(line);
            }
        }

        static void onStop()
        {
            running = false;

            // Stop the DLT
            Node.stop();

            // Stop logging
            Logging.flush();
            Logging.stop();
        }

        public static void stop()
        {
            running = false;
        }
    }
}
