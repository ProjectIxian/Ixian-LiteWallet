using IXICore;
using IXICore.Meta;
using IXICore.Utils;
using LW.Meta;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IxianLiteWallet
{
    class Program
    {
        public static bool noStart = false;

        private static Node node = null;

        static void Main(string[] args)
        {
            // Clear the console first
            Console.Clear();

            Console.WriteLine("Ixian LiteWallet {0} ({1})", Config.version, CoreConfig.version);

            onStart(args);
            mainLoop();
            onStop();
        }

        static void onStart(string[] args)
        {

            // Initialize the crypto manager
            CryptoManager.initLib();

            // Initialize the node
            node = new Node();

            // Start the actual  node
            node.start();
        }

        static void mainLoop()
        {
            bool running = true;
            Console.WriteLine("Type help to see a list of available commands.\n");
            while (running)
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
                    Console.WriteLine("IxianLiteWallet usage:");
                    Console.WriteLine("\texit\t\t\t-exits the litewallet");
                    Console.WriteLine("\thelp\t\t\t-shows this help message");
                    Console.WriteLine("\tbalance\t\t\t-shows this wallet balance");
                    Console.WriteLine("\taddress\t\t\t-shows this wallet's primary address");
                    Console.WriteLine("\taddresses\t\t-shows all addresses for this wallet");
                    Console.WriteLine("\tbackup\t\t\t-backup this wallet as an IXIHEX text");
                    Console.WriteLine("\tchangepass\t\t-changes this wallet's password");
                    Console.WriteLine("\tsend [address] [amount]\t-sends IxiCash to the specified address");
                    // generate new address, view all address balances
                    // change password
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

                if (line.Equals("addresses"))
                {
                    List<Address> address_list = Node.walletStorage.getMyAddresses();

                    Dictionary<string, string> address_balance_list = new Dictionary<string, string>();

                    foreach (Address addr in address_list)
                    {
                        Console.WriteLine("{0}", addr.ToString());
                    }
                    Console.WriteLine("");
                    continue;
                }

                if (line.Equals("backup"))
                {
                    List<byte> wallet = new List<byte>();
                    wallet.AddRange(Node.walletStorage.getRawWallet());
                    Console.WriteLine("IXIHEX" + Crypto.hashToString(wallet.ToArray()));
                    Console.WriteLine("");
                    continue;
                }

                if (line.Equals("changepass"))
                {
                    // Request the current wallet password
                    bool success = false;
                    while (!success)
                    {
                        string password = "";
                        if (password.Length < 10)
                        {
                            Logging.flush();
                            Console.Write("Enter your current wallet password: ");
                            password = ConsoleHelpers.getPasswordInput();
                        }
                        if (password.Length == 0)
                        {
                            break;                          
                        }

                        // Read the wallet using the provided password
                        Node.walletStorage = new WalletStorage(Config.walletFile);
                        if (Node.walletStorage.readWallet(password))
                        {
                            success = true;
                        }
                    }

                    if (success == false)
                        continue;

                    // Request a new password
                    string new_password = "";
                    while (new_password.Length < 10)
                    {
                        new_password = ConsoleHelpers.requestNewPassword("Enter a new password for your wallet: ");
                        if (new_password.Length == 0)
                        {
                            continue;
                        }
                    }
                    if(Node.walletStorage.writeWallet(new_password))
                        Console.WriteLine("Wallet password changed.");
                    continue;
                }

                // Handle multiple parameters
                string[] split = line.Split(new string[] { " " }, StringSplitOptions.None);

                if(split[0].Equals("send"))
                {
                    if(split.Count() < 3)
                    {
                        Console.WriteLine("Incorrect parameters for send. Should be address and amount.\n");
                        continue;
                    }
                    string address = split[1];
                    // Validate the address first
                    byte[] _address = Base58Check.Base58CheckEncoding.DecodePlain(address);
                    if (Address.validateChecksum(_address) == false)
                    {
                        Console.WriteLine("Invalid address checksum!. Please make sure you typed the address correctly.\n");
                        continue;
                    }
                    // Make sure the amount is positive
                    IxiNumber amount = new IxiNumber(split[2]);
                    if (amount < (long)0)
                    {
                        Console.WriteLine("Please type a positive amount.\n");
                        continue;
                    }
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
