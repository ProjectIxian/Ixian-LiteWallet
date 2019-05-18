using DLT;
using DLT.Meta;
using DLT.Network;
using IxianLiteWallet;
using IXICore;
using IXICore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DLT.Meta
{
    class Node
    {
        public static bool running = false;
        public static bool forceShutdown = false;

        public static WalletStorage walletStorage;
        public static WalletState walletState;

        public static IxiNumber balance = 0;      // Stores the last known balance for this node
        public static ulong blockHeight = 0;


        // Perform basic initialization of node
        static public void init()
        {


            running = true;
            Logging.consoleOutput = false;

            // Load or Generate the wallet
            if (!initWallet())
            {
                running = false;
                IxianLiteWallet.Program.noStart = true;
                return;
            }

            Console.WriteLine("Connecting to Ixian network...");
        }

        static public bool initWallet()
        {
            walletStorage = new WalletStorage(Config.walletFile);

            Logging.flush();

            if (!walletStorage.walletExists())
            {
                ConsoleHelpers.displayBackupText();

                // Request a password
                // NOTE: This can only be done in testnet to enable automatic testing!
                string password = "";
                while (password.Length < 10)
                {
                    Logging.flush();
                    password = ConsoleHelpers.requestNewPassword("Enter a password for your new wallet: ");
                    if (forceShutdown)
                    {
                        return false;
                    }
                }
                walletStorage.generateWallet(password);
            }
            else
            {
                ConsoleHelpers.displayBackupText();

                bool success = false;
                while (!success)
                {

                    // NOTE: This is only permitted on the testnet for dev/testing purposes!
                    string password = "";
                    if (password.Length < 10)
                    {
                        Logging.flush();
                        Console.Write("Enter wallet password: ");
                        password = ConsoleHelpers.getPasswordInput();
                    }
                    if (forceShutdown)
                    {
                        return false;
                    }
                    if (walletStorage.readWallet(password))
                    {
                        success = true;
                    }
                }
            }


            if (walletStorage.getPrimaryPublicKey() == null)
            {
                return false;
            }

            // Wait for any pending log messages to be written
            Logging.flush();

            Console.WriteLine();
            Console.WriteLine("Your IXIAN addresses are: ");
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var entry in walletStorage.getMyAddressesBase58())
            {
                Console.WriteLine(entry);
            }
            Console.ResetColor();
            Console.WriteLine();

            if (Config.onlyShowAddresses)
            {
                return false;
            }

    /*        // Check if we should change the password of the wallet
            if (Config.changePass == true)
            {
                // Request a new password
                string new_password = "";
                while (new_password.Length < 10)
                {
                    new_password = ConsoleHelpers.requestNewPassword("Enter a new password for your wallet: ");
                    if (forceShutdown)
                    {
                        return false;
                    }
                }
                walletStorage.writeWallet(new_password);
            }*/


            return true;
        }

        static public void stop()
        {
            Program.noStart = true;
            forceShutdown = true;
            ConsoleHelpers.forceShutdown = true;

            // Stop the network queue
            NetworkQueue.stop();

            // Stop all network clients
            NetworkClientManager.stop();
        }

        static public void start(bool verboseConsoleOutput)
        {
            PresenceList.generatePresenceList(Config.publicServerIP, 'C');

            // Start the network queue
            NetworkQueue.start();

            // Start the network client manager
            NetworkClientManager.start();

        }

        static public void reconnect()
        {

            // Reset the network receive queue
            NetworkQueue.reset();

            NetworkClientManager.restartClients();
        }


        static public void getBalance()
        {
            ProtocolMessage.setWaitFor(ProtocolMessageCode.balance);

            // Return the balance for the matching address
            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.Write(Node.walletStorage.getPrimaryAddress().Length);
                    writer.Write(Node.walletStorage.getPrimaryAddress());
                    NetworkClientManager.broadcastData(new char[] { 'M' }, ProtocolMessageCode.getBalance, mw.ToArray(), null);
                }
            }
            ProtocolMessage.wait();
        }

        static public void sendTransaction(string address, IxiNumber amount)
        {
            SortedDictionary<byte[], IxiNumber> to_list = new SortedDictionary<byte[], IxiNumber>(new ByteArrayComparer());

            IxiNumber fee = CoreConfig.transactionPrice;
            byte[] from = Node.walletStorage.getPrimaryAddress();
            byte[] pubKey = Node.walletStorage.getPrimaryPublicKey();
            Transaction transaction = new Transaction((int)Transaction.Type.Normal, fee, to_list, from, null, pubKey, Node.getLastBlockHeight());
        }

        static public bool update()
        {
            return running;
        }

        public static string getFullAddress()
        {
            return Config.publicServerIP + ":" + Config.serverPort;
        }

        public static int getLastBlockVersion()
        {
            return 0;
        }

        public static ulong getLastBlockHeight()
        {
            return 0;
        }

        public static Block getLastBlock()
        {
            return null;
        }

        public static int getRequiredConsensus()
        {
            return 0;
        }

        public static bool isAcceptingConnections()
        {
            return false;
        }

        public static char getNodeType()
        {
            return 'C';
        }
    }
}
