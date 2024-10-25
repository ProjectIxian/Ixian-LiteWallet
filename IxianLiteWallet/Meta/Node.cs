using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.RegNames;
using IXICore.Utils;
using LW.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static IXICore.Transaction;

namespace LW.Meta
{
    class Balance
    {
        public Address address = null;
        public IxiNumber balance = 0;
        public ulong blockHeight = 0;
        public byte[] blockChecksum = null;
        public bool verified = false;

        public Balance(Address address, IxiNumber balance)
        {
            this.address = address;
            this.balance = balance;
        }
    }

    class Node : IxianNode
    {
        public static bool running = false;

        public static List<Balance> balances = new List<Balance>(); // Stores the last known balances for this node

        public static TransactionInclusion tiv = null;

        public static ulong networkBlockHeight = 0;
        public static byte[] networkBlockChecksum = null;
        public static int networkBlockVersion = 0;
        private bool generatedNewWallet = false;

        public Node()
        {
            CoreConfig.simultaneousConnectedNeighbors = 6;
            IxianHandler.init(Config.version, this, NetworkType.main, true);
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
                Logging.error("Viewing-only wallet {0} cannot be used as the primary wallet.", walletStorage.getPrimaryAddress().ToString());
                return false;
            }

            IxianHandler.addWallet(walletStorage);

            // Prepare the balances list
            List<Address> address_list = IxianHandler.getWalletStorage().getMyAddresses();
            foreach (Address addr in address_list)
            {
                balances.Add(new Balance(addr, 0));
            }

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
                tiv.start("", 0, null, false);
            }
        }

        static public void getBalance(byte[] address)
        {
            ProtocolMessage.setWaitFor(ProtocolMessageCode.balance2, address);

            // Return the balance for the matching address
            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.WriteIxiVarInt(address.Length);
                    writer.Write(address);
                    NetworkClientManager.broadcastData(new char[] { 'M', 'H' }, ProtocolMessageCode.getBalance2, mw.ToArray(), null);
                }
            }
            ProtocolMessage.wait(30);
        }

        static public void generateNewAddress()
        {
            Address base_address = IxianHandler.getWalletStorage().getPrimaryAddress();
            Address new_address = IxianHandler.getWalletStorage().generateNewAddress(base_address, null);
            if (new_address != null)
            {
                balances.Add(new Balance(new_address, 0));
                Console.WriteLine("New address generated: {0}", new_address.ToString());
            }
            else
            {
                Console.WriteLine("Error occurred while generating a new address");
            }
        }

        static public void sendTransaction(Address address, IxiNumber amount)
        {
            // TODO add support for sending funds from multiple addreses automatically based on remaining balance
            Balance address_balance = balances.First();
            var from = address_balance.address;
            sendTransactionFrom(from, address, amount);
        }

        static public void sendTransactionFrom(Address fromAddress, Address address, IxiNumber amount)
        {
            getBalance(fromAddress.addressWithChecksum);

            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            SortedDictionary<Address, ToEntry> to_list = new(new AddressComparer());
            Balance address_balance = balances.FirstOrDefault(addr => addr.address.addressWithChecksum.SequenceEqual(fromAddress.addressWithChecksum));
            Address pubKey = new(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            if (!IxianHandler.getWalletStorage().isMyAddress(fromAddress))
            {
                Console.WriteLine("From address is not my address.\n");
                return;
            }

            SortedDictionary<byte[], IxiNumber> from_list = new(new ByteArrayComparer())
            {
                { IxianHandler.getWalletStorage().getAddress(fromAddress).nonce, amount }
            };

            to_list.AddOrReplace(address, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), amount));

            // Prepare transaction to calculate fee
            Transaction transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            byte[] first_address = from_list.Keys.First();
            from_list[first_address] = from_list[first_address] + transaction.fee;
            IxiNumber wal_bal = IxianHandler.getWalletBalance(new Address(transaction.pubKey.addressNoChecksum, first_address));
            if (from_list[first_address] > wal_bal)
            {
                IxiNumber maxAmount = wal_bal - transaction.fee;

                if (maxAmount < 0)
                    maxAmount = 0;

                Console.WriteLine("Insufficient funds to cover amount and transaction fee.\nMaximum amount you can send is {0} IXI.\n", maxAmount);
                return;
            }
            // Prepare transaction with updated "from" amount to cover fee
            transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            // Send the transaction
            if (IxianHandler.addTransaction(transaction, true))
            {
                Console.WriteLine("Sending transaction, txid: {0}\n", transaction.getTxIdString());
            }
            else
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
            Console.WriteLine("Transaction {0} is {1}\n", Transaction.getTxIdString(txid), status);
        }

        public override void receivedBlockHeader(Block block_header, bool verified)
        {
            foreach (Balance balance in balances)
            {
                if (balance.blockChecksum != null && balance.blockChecksum.SequenceEqual(block_header.blockChecksum))
                {
                    balance.verified = true;
                }
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
            if (tiv.getLastBlockHeader() == null 
                || tiv.getLastBlockHeader().version < Block.maxVersion)
            {
                // TODO Omega force to v10 after upgrade
                return Block.maxVersion - 1;
            }
            return tiv.getLastBlockHeader().version;
        }

        public override bool addTransaction(Transaction tx, bool force_broadcast)
        {
            // TODO Send to peer if directly connectable
            CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.transactionData2, tx.getBytes(true, true), null);
            PendingTransactions.addPendingLocalTransaction(tx);
            return true;
        }

        public override Block getLastBlock()
        {
            return tiv.getLastBlockHeader();
        }

        public override Wallet getWallet(Address id)
        {
            foreach (Balance balance in balances)
            {
                if (id.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                    return new Wallet(id, balance.balance);
            }
            return new Wallet(id, 0);
        }

        public override IxiNumber getWalletBalance(Address id)
        {
            foreach (Balance balance in balances)
            {
                if (id.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
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
                        Console.WriteLine("Error sending the transaction {0}", t.getTxIdString());
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    if (cur_time - tx_time > 40) // if the transaction is pending for over 40 seconds, resend
                    {
                        CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.transactionData2, t.getBytes(true), null);
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

        public override Block getBlockHeader(ulong blockNum)
        {
            return BlockHeaderStorage.getBlockHeader(blockNum);
        }

        public override IxiNumber getMinSignerPowDifficulty(ulong blockNum, long curBlockTimestamp)
        {
            // TODO TODO implement this properly
            return ConsensusConfig.minBlockSignerPowDifficulty;
        }


        public override byte[] getBlockHash(ulong blockNum)
        {
            Block b = getBlockHeader(blockNum);
            if (b == null)
            {
                return null;
            }

            return b.blockChecksum;
        }

        public override RegisteredNameRecord getRegName(byte[] name, bool useAbsoluteId)
        {
            throw new NotImplementedException();
        }
    }
}
