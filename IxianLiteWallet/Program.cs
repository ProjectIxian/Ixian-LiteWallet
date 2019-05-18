using DLT;
using DLT.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IxianLiteWallet
{
    class Program
    {
        public static bool noStart = false;

        static void Main(string[] args)
        {
            // Clear the console first
            Console.Clear();

            Console.WriteLine("Ixian LiteWallet {0}", Config.version);

            onStart(args);
            mainLoop();
            onStop();
        }

        static void onStart(string[] args)
        {

            // Initialize the crypto manager
            CryptoManager.initLib();

            // Initialize the node
            Node.init();

            // Start the actual  node
            Node.start(false);
        }

        static void mainLoop()
        {
            bool running = true;
            while(running)
            {
                Console.Write("IxianLiteWallet>");
                string line = Console.ReadLine();
                Console.WriteLine("");

                if (line.Equals("exit") || line.Equals("quit"))
                {
                    running = false;
                    continue;
                }

                if(line.Equals("help"))
                {
                    Console.WriteLine("Ixian LiteWallet usage:");
                    Console.WriteLine("\texit\t\t\t-exits the litewallet");
                    Console.WriteLine("\thelp\t\t\t-shows this help message");
                    Console.WriteLine("\tbalance\t\t\t-shows this wallet balance");
                    Console.WriteLine("\taddress\t\t\t-shows this wallet's primary address");
                    Console.WriteLine("\tsend [address] [amount]\t-sends IxiCash to the specified address");

                    Console.WriteLine("");
                    continue;
                }

                if (line.Equals("balance"))
                {
                    Node.getBalance();
                    Console.WriteLine("Balance: {0} IXI\n", Node.balance);
                    continue;
                }

                if (line.Equals("address"))
                {                 
                    Console.WriteLine("Primary address: {0}\n", Base58Check.Base58CheckEncoding.EncodePlain(Node.walletStorage.getPrimaryAddress()));
                    continue;
                }

                string[] split = line.Split(new string[] { " " }, StringSplitOptions.None);

                if(split[0].Equals("send"))
                {
                    if(split.Count() < 3)
                    {
                        Console.WriteLine("Incorrect parameters for send. Should be address and amount.\n");
                        continue;
                    }
                    string address = split[1];
                    IxiNumber amount = new IxiNumber(split[2]);
                    Node.sendTransaction(address, amount);
                    continue;
                }

            }
        }

        static void onStop()
        {
            if (noStart == false)
            {
                // Stop the DLT
                Node.stop();
            }
            // Stop logging
            Logging.flush();
            Logging.stop();
        }
    }
}
