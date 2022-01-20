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
        public byte[] address = null;
        public IxiNumber balance = 0;
        public ulong blockHeight = 0;
        public byte[] blockChecksum = null;
        public bool verified = false;
    }

    class Node : IxianNode
    {
        public static bool running = false;

        public static Balance balance = new Balance();      // Stores the last known balance for this node

        public static TransactionInclusion tiv = null;

        public static ulong networkBlockHeight = 0;
        public static byte[] networkBlockChecksum = null;
        public static int networkBlockVersion = 0;
        private bool generatedNewWallet = false;

        public Node()
        {
            CoreConfig.simultaneousConnectedNeighbors = 6;
            IxianHandler.init(Config.version, this, NetworkType.main);
            init();
        }

        // Perform basic initialization of node
        private void init()
        {
            Logging.consoleOutput = false;

            running = true;

            // Load or Generate the wallet
            if (!initWallet())
            {
                running = false;
                IxianLiteWallet.Program.running = false;
                return;
            }

            Console.WriteLine("Connecting to Ixian network...");

            PeerStorage.init("");

            // Init TIV
            tiv = new TransactionInclusion();
        }

        private bool initWallet()
        {
            WalletStorage walletStorage = new WalletStorage(Config.walletFile);

            Logging.flush();

            if (!walletStorage.walletExists())
            {
                ConsoleHelpers.displayBackupText();

                // Request a password
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
                generatedNewWallet = true;
            }
            else
            {
                ConsoleHelpers.displayBackupText();

                bool success = false;
                while (!success)
                {

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

            if (walletStorage.viewingWallet)
            {
                Logging.error("Viewing-only wallet {0} cannot be used as the primary wallet.", Base58Check.Base58CheckEncoding.EncodePlain(walletStorage.getPrimaryAddress()));
                return false;
            }

            IxianHandler.addWallet(walletStorage);

            return true;
        }

        static public void stop()
        {
            IxianHandler.forceShutdown = true;

            // Stop TIV
            tiv.stop();

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
            NetworkClientManager.start(2);

            // Start TIV
            if (generatedNewWallet || !File.Exists(Config.walletFile))
            {
                generatedNewWallet = false;
                tiv.start("");
            }
            else
            {
                tiv.start("", 0, null);
            }
        }

        static public void getBalance()
        {
            ProtocolMessage.setWaitFor(ProtocolMessageCode.balance2);

            // Return the balance for the matching address
            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.WriteIxiVarInt(IxianHandler.getWalletStorage().getPrimaryAddress().Length);
                    writer.Write(IxianHandler.getWalletStorage().getPrimaryAddress());
                    NetworkClientManager.broadcastData(new char[] { 'M', 'H' }, ProtocolMessageCode.getBalance2, mw.ToArray(), null);
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
            byte[] from = IxianHandler.getWalletStorage().getPrimaryAddress();
            byte[] pubKey = IxianHandler.getWalletStorage().getPrimaryPublicKey();
            to_list.AddOrReplace(Base58Check.Base58CheckEncoding.DecodePlain(address), amount);
            Transaction transaction = new Transaction((int)Transaction.Type.Normal, fee, to_list, from, null, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            if (IxianHandler.addTransaction(transaction, true))
            {
                Console.WriteLine("Sending transaction, txid: {0}\n", Transaction.txIdV8ToLegacy(transaction.id));
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

        public override void receivedTransactionInclusionVerificationResponse(byte[] txid, bool verified)
        {
            string status = "NOT VERIFIED";
            if (verified)
            {
                status = "VERIFIED";
                PendingTransactions.remove(txid);
            }
            Console.WriteLine("Transaction {0} is {1}\n", Transaction.txIdV8ToLegacy(txid), status);
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
            processPendingTransactions();
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
            if (tiv.getLastBlockHeader() == null || tiv.getLastBlockHeader().version < Block.maxVersion)
            {
                return Block.maxVersion - 1;
            }
            return tiv.getLastBlockHeader().version;
        }

        public override bool addTransaction(Transaction tx, bool force_broadcast)
        {
            // TODO Send to peer if directly connectable
            CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.transactionData, tx.getBytes(), null);
            PendingTransactions.addPendingLocalTransaction(tx);
            return true;
        }

        public override Block getLastBlock()
        {
            throw new NotImplementedException();
        }

        public override Wallet getWallet(byte[] id)
        {
            // TODO Properly implement this for multiple addresses
            if (balance.address != null && id.SequenceEqual(balance.address))
            {
                return new Wallet(balance.address, balance.balance);
            }
            return new Wallet(id, 0);
        }

        public override IxiNumber getWalletBalance(byte[] id)
        {
            // TODO Properly implement this for multiple addresses
            if (balance.address != null && id.SequenceEqual(balance.address))
            {
                return balance.balance;
            }
            return 0;
        }

        public override void shutdown()
        {
            IxianHandler.forceShutdown = true;
        }

        public override void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            ProtocolMessage.parseProtocolMessage(code, data, endpoint);
        }

        public static void processPendingTransactions()
        {
            // TODO TODO improve to include failed transactions
            ulong last_block_height = IxianHandler.getLastBlockHeight();
            lock (PendingTransactions.pendingTransactions)
            {
                long cur_time = Clock.getTimestamp();
                List<PendingTransaction> tmp_pending_transactions = new List<PendingTransaction>(PendingTransactions.pendingTransactions);
                int idx = 0;
                foreach (var entry in tmp_pending_transactions)
                {
                    Transaction t = entry.transaction;
                    long tx_time = entry.addedTimestamp;

                    if (t.applied != 0)
                    {
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    // if transaction expired, remove it from pending transactions
                    if (last_block_height > ConsensusConfig.getRedactedWindowSize() && t.blockHeight < last_block_height - ConsensusConfig.getRedactedWindowSize())
                    {
                        Console.WriteLine("Error sending the transaction {0}", Transaction.txIdV8ToLegacy(t.id));
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    if (cur_time - tx_time > 40) // if the transaction is pending for over 40 seconds, resend
                    {
                        CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.transactionData, t.getBytes(), null);
                        entry.addedTimestamp = cur_time;
                        entry.confirmedNodeList.Clear();
                    }

                    if (entry.confirmedNodeList.Count() > 3) // already received 3+ feedback
                    {
                        continue;
                    }

                    if (cur_time - tx_time > 20) // if the transaction is pending for over 20 seconds, send inquiry
                    {
                        CoreProtocolMessage.broadcastGetTransaction(t.id, 0);
                    }

                    idx++;
                }
            }
        }

        public override BlockHeader getBlockHeader(ulong blockNum)
        {
            return BlockHeaderStorage.getBlockHeader(blockNum);
        }
    }
}
