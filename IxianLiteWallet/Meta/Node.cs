using IxianLiteWallet;
using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using LW.Network;
using System;
using System.Collections.Generic;
using System.IO;

namespace LW.Meta
{
    class Node : IxianNode
    {
        public static bool running = false;

        public static WalletStorage walletStorage;

        public static IxiNumber balance = 0;      // Stores the last known balance for this node
        public static ulong blockHeight = 0;
        public static int blockVersion = 0;

        public Node()
        {
            CoreConfig.productVersion = Config.version;
            IxianHandler.setHandler(this);
            init();
        }

        // Perform basic initialization of node
        private void init()
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

        private bool initWallet()
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
                    if (IxianHandler.forceShutdown)
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
                    if (IxianHandler.forceShutdown)
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

            return true;
        }

        static public void stop()
        {
            Program.noStart = true;
            IxianHandler.forceShutdown = true;

            // Stop the keepalive thread
            //PresenceList.stopKeepAlive();

            // Stop the network queue
            NetworkQueue.stop();

            // Stop all network clients
            NetworkClientManager.stop();
        }

        public void start()
        {
            PresenceList.init(IxianHandler.publicIP, 0, 'C');

            // Start the network queue
            NetworkQueue.start();

            // Start the network client manager
            NetworkClientManager.start();

            // Start the keepalive thread
            //PresenceList.startKeepAlive();

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
            ProtocolMessage.wait(30);
        }

        static public void sendTransaction(string address, IxiNumber amount)
        {
            Node.getBalance();

            if (Node.balance < amount)
            {
                Console.WriteLine("Insufficient funds.\n");
                return;
            }

            SortedDictionary<byte[], IxiNumber> to_list = new SortedDictionary<byte[], IxiNumber>(new ByteArrayComparer());

            IxiNumber fee = ConsensusConfig.transactionPrice;
            byte[] from = Node.walletStorage.getPrimaryAddress();
            byte[] pubKey = Node.walletStorage.getPrimaryPublicKey();
            to_list.AddOrReplace(Base58Check.Base58CheckEncoding.DecodePlain(address), amount);
            Transaction transaction = new Transaction((int)Transaction.Type.Normal, fee, to_list, from, null, pubKey, Node.blockHeight);
            if (IxianHandler.addTransaction(transaction))
            {
                Console.WriteLine("Transaction sent: {0}\n", transaction.id);
            }else
            {
                Console.WriteLine("Could not send transaction\n");
            }

        }

        public override ulong getLastBlockHeight()
        {
            return Node.blockHeight;
        }

        public override bool isAcceptingConnections()
        {
            return false;
        }

        public override ulong getHighestKnownNetworkBlockHeight()
        {
            return Node.blockHeight;
        }

        public override int getLastBlockVersion()
        {
            return Node.blockVersion;
        }

        public override bool addTransaction(Transaction tx)
        {
            // TODO use pending transactions here
            return NetworkClientManager.broadcastData(new char[] { 'M' }, ProtocolMessageCode.newTransaction, tx.getBytes(), null);
        }

        public override Block getLastBlock()
        {
            throw new NotImplementedException();
        }

        public override Wallet getWallet(byte[] id)
        {
            throw new NotImplementedException();
        }

        public override IxiNumber getWalletBalance(byte[] id)
        {
            throw new NotImplementedException();
        }

        public override void shutdown()
        {
            IxianHandler.forceShutdown = true;
        }

        public override WalletStorage getWalletStorage()
        {
            return walletStorage;
        }

        public override void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            ProtocolMessage.parseProtocolMessage(code, data, endpoint);
        }
    }
}
