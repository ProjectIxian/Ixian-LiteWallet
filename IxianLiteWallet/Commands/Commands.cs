using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using LW.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace IxianLiteWallet
{
    class Commands
    {
        public bool stressRunning { get; private set; } = false;
        int stressTargetTps = 5;
        int stressTxCount = 100;

        public Commands()
        {

        }

        public void handleCommand(string line)
        {
            line = line.Trim();
            int ws_index = line.IndexOf(' ');
            if(ws_index == -1)
            {
                ws_index = line.Length;
            }
            string command = line.Substring(0, ws_index).ToLower();
            switch(command)
            {
                case "exit":
                case "quit":
                    Program.stop();
                    break;

                case "help":
                    handleHelp();
                    break;

                case "status":
                    handleStatus();
                    break;

                case "balance":
                    handleBalance();
                    break;

                case "address":
                    handleAddress();
                    break;

                case "addresses":
                    handleAddresses();
                    break;

                case "backup":
                    handleBackup();
                    break;

                case "changepass":
                    handleChangePass();
                    break;

                case "send":
                    handleSend(line);
                    break;

                case "verify":
                    handleVerify(line);
                    break;

                case "stress":
                    handleStress(line);
                    break;
            }
        }

        void handleHelp()
        {
            Console.WriteLine("Ixian LiteWallet usage:");
            Console.WriteLine("\texit\t\t\t-exits the litewallet");
            Console.WriteLine("\thelp\t\t\t-shows this help message");
            Console.WriteLine("\tstatus\t\t\t-shows the number of connected DLT nodes");
            Console.WriteLine("\tbalance\t\t\t-shows this wallet balance");
            Console.WriteLine("\taddress\t\t\t-shows this wallet's primary address");
            Console.WriteLine("\taddresses\t\t-shows all addresses for this wallet");
            Console.WriteLine("\tbackup\t\t\t-backup this wallet as an IXIHEX text");
            Console.WriteLine("\tchangepass\t\t-changes this wallet's password");
            //Console.WriteLine("\tverify [txid]\t\t-verifies the specified transaction txid");
            Console.WriteLine("\tsend [address] [amount]\t-sends IxiCash to the specified address");
            // generate new address, view all address balances
            // change password
            Console.WriteLine("");
        }

        void handleBalance()
        {
            Node.getBalance();
            string verified = "";
            if (Node.balance.verified)
            {
                //verified = " (verified)"; // not yet
            }
            Console.WriteLine("Balance: {0} IXI{1}\n", Node.balance.balance, verified);
        }

        void handleAddress()
        {
            Console.WriteLine("Primary address: {0}\n", Base58Check.Base58CheckEncoding.EncodePlain(IxianHandler.getWalletStorage().getPrimaryAddress()));
        }

        void handleAddresses()
        {
            List<Address> address_list = IxianHandler.getWalletStorage().getMyAddresses();

            foreach (Address addr in address_list)
            {
                Console.WriteLine("{0}", addr.ToString());
            }
            Console.WriteLine("");
        }

        void handleBackup()
        {
            List<byte> wallet = new List<byte>();
            wallet.AddRange(IxianHandler.getWalletStorage().getRawWallet());
            Console.WriteLine("IXIHEX" + Crypto.hashToString(wallet.ToArray()));
            Console.WriteLine("");
        }

        void handleChangePass()
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
                if (IxianHandler.getWalletStorage().isValidPassword(password))
                {
                    success = true;
                }
            }

            if (success == false)
                return;

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
            if (IxianHandler.getWalletStorage().writeWallet(new_password))
                Console.WriteLine("Wallet password changed.");
        }

        void handleSend(string line)
        {
            string[] split = line.Split(new string[] { " " }, StringSplitOptions.None);
            if (split.Count() < 3)
            {
                Console.WriteLine("Incorrect parameters for send. Should be address and amount.\n");
                return;
            }
            string address = split[1];
            // Validate the address first
            byte[] _address = Base58Check.Base58CheckEncoding.DecodePlain(address);
            if (Address.validateChecksum(_address) == false)
            {
                Console.WriteLine("Invalid address checksum!. Please make sure you typed the address correctly.\n");
                return;
            }
            // Make sure the amount is positive
            IxiNumber amount = new IxiNumber(split[2]);
            if (amount < (long)0)
            {
                Console.WriteLine("Please type a positive amount.\n");
                return;
            }
            Node.sendTransaction(address, amount);
        }

        void handleVerify(string line)
        {
            string[] split = line.Split(new string[] { " " }, StringSplitOptions.None);
            if (split.Count() < 2)
            {
                Console.WriteLine("Incorrect parameters for verify. Should be at least the txid.\n");
                return;
            }

            string txid = split[1];

            int connectionsOut = NetworkClientManager.getConnectedClients(true).Count();
            if (connectionsOut < 3)
            {
                Console.WriteLine("Need at least 3 node connections to verify transactions.");
                return;
            }

            Console.WriteLine("Posting Transaction Inclusion Verification request for {0}", txid);

            // TODO
            //tiv.verifyTransactionInclusion(txid);
        }

        void handleStatus()
        {
            Console.WriteLine("Last Block Height: {0}", IxianHandler.getLastBlockHeight());
            Console.WriteLine("Network Block Height: {0}", IxianHandler.getHighestKnownNetworkBlockHeight());

            int connectionsOut = NetworkClientManager.getConnectedClients(true).Count();
            Console.WriteLine("Connections: {0}", connectionsOut);

            Console.WriteLine("Pending transactions: {0}\n", PendingTransactions.pendingTransactionCount());
        }

        void handleStress(string line)
        {
            string[] split = line.Split(new string[] { " " }, StringSplitOptions.None);
            if (split.Count() < 4)
            {
                Console.WriteLine("Incorrect parameters for stress test, TPS, total transactions and to address is required.\n");
                return;
            }

            stressTargetTps = Int32.Parse(split[1]);
            if(stressTargetTps > 50)
            {
                stressTargetTps = 50;
            }

            stressTxCount = Int32.Parse(split[2]);
            if(stressTxCount > 10000)
            {
                stressTxCount = 10000;
            }

            if (stressRunning == true)
            {
                return;
            }

            byte[] to = Base58Check.Base58CheckEncoding.DecodePlain(split[3]);

            new Thread(() =>
           {
               Thread.CurrentThread.IsBackground = true;

               if(stressRunning == true)
               {
                   return;
               }

               stressRunning = true;
               try
               {
                   IxiNumber amount = ConsensusConfig.transactionPrice;
                   IxiNumber fee = ConsensusConfig.transactionPrice;
                   byte[] from = IxianHandler.getWalletStorage().getPrimaryAddress();
                   byte[] pubKey = IxianHandler.getWalletStorage().getPrimaryPublicKey();

                   long start_time = Clock.getTimestampMillis();
                   int spam_counter = 0;
                   for (int i = 0; i < stressTxCount; i++)
                   {
                       Transaction transaction = new Transaction((int)Transaction.Type.Normal, amount, fee, to, from, null, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
                       IxianHandler.addTransaction(transaction, true);

                       spam_counter++;
                       if (spam_counter >= stressTargetTps)
                       {
                           Console.WriteLine("Stress: Sent " + spam_counter + " transactions.");
                           long elapsed = Clock.getTimestampMillis() - start_time;
                           if (elapsed < 1000)
                           {
                               Thread.Sleep(1000 - (int)elapsed);
                           }
                           spam_counter = 0;
                           start_time = Clock.getTimestampMillis();
                       }
                   }
               }
               catch (Exception e)
               {
                   Logging.error("Exception occured during stress test: {0}", e);
               }
               stressRunning = false;
           }).Start();
        }
    }
}
