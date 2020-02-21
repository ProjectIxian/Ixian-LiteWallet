using IxianLiteWallet;
using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using LW.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LW.Meta
{
    class Balance
    {
        public IxiNumber balance = 0;
        public ulong blockHeight = 0;
        public byte[] blockChecksum = null;
        public bool verified = false;
    }

    class Node : IxianNode
    {
        public static bool running = false;

        public static WalletStorage walletStorage;

        public static Balance balance = new Balance();      // Stores the last known balance for this node

        public static TransactionInclusion tiv = null;

        public static ulong networkBlockHeight = 0;
        public static byte[] networkBlockChecksum = null;
        public static int networkBlockVersion = 0;

        public Node()
        {
            CoreConfig.productVersion = Config.version;
            IxianHandler.setHandler(this);
            init();
        }

        // Perform basic initialization of node
        private void init()
        {
            Logging.consoleOutput = false;

            CoreConfig.isTestNet = false;

            running = true;

            // Load or Generate the wallet
            if (!initWallet())
            {
                running = false;
                IxianLiteWallet.Program.noStart = true;
                return;
            }

            Console.WriteLine("Connecting to Ixian network...");

            // Start TIV
            tiv = new TransactionInclusion();
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

            // Stop TIV
            tiv.stop();

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

        static public void test()
        {

        }

        static public void verifyTransaction(string txid)
        {
             int connectionsOut = NetworkClientManager.getConnectedClients(true).Count();
             if(connectionsOut < 3)
             {
                 Console.WriteLine("Need at least 3 node connections to verify transactions.");
                 return;
             }

            Console.WriteLine("Posting Transaction Inclusion Verification request for {0}", txid);

            // TODO
            //tiv.verifyTransactionInclusion(txid);
        }

        static public void status()
        {
            Console.WriteLine("Last Block Height: {0}", IxianHandler.getLastBlockHeight());
            Console.WriteLine("Network Block Height: {0}", IxianHandler.getHighestKnownNetworkBlockHeight());

            int connectionsOut = NetworkClientManager.getConnectedClients(true).Count();
            Console.WriteLine("Connections: {0}\n", connectionsOut);
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

            if (Node.balance.balance < amount)
            {
                Console.WriteLine("Insufficient funds.\n");
                return;
            }

            SortedDictionary<byte[], IxiNumber> to_list = new SortedDictionary<byte[], IxiNumber>(new ByteArrayComparer());

            IxiNumber fee = ConsensusConfig.transactionPrice;
            byte[] from = Node.walletStorage.getPrimaryAddress();
            byte[] pubKey = Node.walletStorage.getPrimaryPublicKey();
            to_list.AddOrReplace(Base58Check.Base58CheckEncoding.DecodePlain(address), amount);
            Transaction transaction = new Transaction((int)Transaction.Type.Normal, fee, to_list, from, null, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            if (IxianHandler.addTransaction(transaction))
            {
                Console.WriteLine("Transaction sent: {0}\n", transaction.id);
            }else
            {
                Console.WriteLine("Could not send transaction\n");
            }

        }

        static public void setNetworkBlock(ulong block_height, byte[] block_checksum, int block_version)
        {
            networkBlockHeight = block_height;
            networkBlockChecksum = block_checksum;
            networkBlockVersion = block_version;
        }

        public override void receivedTransactionInclusionVerificationResponse(string txid, bool verified)
        {
            string status = "VERIFIED";
            if (!verified)
                status = "NOT VERIFIED";

            Console.WriteLine("Transaction {0} is {1}\n",txid, status);
        }

        public override void receivedBlockHeader(BlockHeader block_header, bool verified)
        {
            if(balance.blockChecksum != null && balance.blockChecksum.SequenceEqual(block_header.blockChecksum))
            {
                balance.verified = true;
            }
            if(block_header.blockNum >= networkBlockHeight)
            {
                IxianHandler.status = NodeStatus.ready;
                setNetworkBlock(block_header.blockNum, block_header.blockChecksum, block_header.version);
            }
        }

        public override ulong getLastBlockHeight()
        {
            if(tiv.getLastBlockHeader() == null)
            {
                return 0;
            }
            return tiv.getLastBlockHeader().blockNum;
        }

        public override bool isAcceptingConnections()
        {
            return false;
        }

        public override ulong getHighestKnownNetworkBlockHeight()
        {
            return networkBlockHeight;
        }

        public override int getLastBlockVersion()
        {
            if (tiv.getLastBlockHeader() == null)
            {
                return 0;
            }
            if(tiv.getLastBlockHeader().version < BlockVer.v6)
            {
                return BlockVer.v6;
            }
            return tiv.getLastBlockHeader().version;
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
