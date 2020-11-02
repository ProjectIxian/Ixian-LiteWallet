using IXICore;
using IXICore.Meta;
using LW.Meta;
using System;

namespace IxianLiteWallet
{
    class Program
    {
        public static bool running = false;

        private static Node node = null;

        static void Main(string[] args)
        {
            // Clear the console first
            Console.Clear();

            Console.WriteLine("Ixian Lite Wallet {0} ({1})", Config.version, CoreConfig.version);

            onStart(args);
            mainLoop();
            onStop();
        }

        static void onStart(string[] args)
        {
            running = true;

            // Initialize the node
            node = new Node();

            // Start the node
            node.start();
        }

        static void mainLoop()
        {
            Console.WriteLine("Type help to see a list of available commands.\n");
            var cmd = new Commands();
            while (running && !IxianHandler.forceShutdown)
            {
                Console.Write("IxianLiteWallet>");
                string line = Console.ReadLine();
                Console.WriteLine("");

                cmd.handleCommand(line);
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
