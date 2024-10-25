using IxianLiteWallet;
using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using LW.Meta;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace LW.Network
{
    public class ProtocolMessage
    {
        public static ProtocolMessageCode waitingFor = 0;
        public static byte[] waitForAddress = null;
        public static bool blocked = false;

        public static void setWaitFor(ProtocolMessageCode value, byte[] addr)
        {
            waitingFor = value;
            waitForAddress = addr;
            blocked = true;
        }

        public static void wait(int timeout_seconds)
        {
            DateTime start = DateTime.UtcNow;
            while(blocked)
            {
                if((DateTime.UtcNow - start).TotalSeconds > timeout_seconds)
                {
                    Logging.warn("Timeout occured while waiting for " + waitingFor);
                    break;
                }
                Thread.Sleep(250);
            }
        }

        // Unified protocol message parsing
        public static void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            if (endpoint == null)
            {
                Logging.error("Endpoint was null. parseProtocolMessage");
                return;
            }
            try
            {
                switch (code)
                {
                    case ProtocolMessageCode.hello:
                        using (MemoryStream m = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(m))
                            {
                                CoreProtocolMessage.processHelloMessageV6(endpoint, reader);
                            }
                        }
                        break;


                    case ProtocolMessageCode.helloData:
                        using (MemoryStream m = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(m))
                            {
                                if (CoreProtocolMessage.processHelloMessageV6(endpoint, reader))
                                {
                                    char node_type = endpoint.presenceAddress.type;
                                    if (node_type != 'M' && node_type != 'H')
                                    {
                                        CoreProtocolMessage.sendBye(endpoint, ProtocolByeCode.expectingMaster, string.Format("Expecting master node."), "", true);
                                        return;
                                    }

                                    ulong last_block_num = reader.ReadIxiVarUInt();

                                    int bcLen = (int)reader.ReadIxiVarUInt();
                                    byte[] block_checksum = reader.ReadBytes(bcLen);

                                    endpoint.blockHeight = last_block_num;

                                    int block_version = (int)reader.ReadIxiVarUInt();

                                    // Process the hello data
                                    endpoint.helloReceived = true;
                                    NetworkClientManager.recalculateLocalTimeDifference();

                                    if (endpoint.presenceAddress.type == 'M' || endpoint.presenceAddress.type == 'H')
                                    {
                                        Node.setNetworkBlock(last_block_num, block_checksum, block_version);

                                        // Get random presences
                                        endpoint.sendData(ProtocolMessageCode.getRandomPresences, new byte[1] { (byte)'M' });
                                        endpoint.sendData(ProtocolMessageCode.getRandomPresences, new byte[1] { (byte)'H' });

                                        CoreProtocolMessage.subscribeToEvents(endpoint);
                                    }
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.balance2:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    int address_length = (int)reader.ReadIxiVarUInt();
                                    Address address = new Address(reader.ReadBytes(address_length));

                                    int balance_bytes_len = (int)reader.ReadIxiVarUInt();
                                    byte[] balance_bytes = reader.ReadBytes(balance_bytes_len);

                                    // Retrieve the latest balance
                                    IxiNumber ixi_balance = new IxiNumber(new BigInteger(balance_bytes));

                                    foreach (Balance balance in Node.balances)
                                    {
                                        if (address.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                                        {
                                            // Retrieve the blockheight for the balance
                                            ulong block_height = reader.ReadIxiVarUInt();

                                            if (block_height > balance.blockHeight && (balance.balance != ixi_balance || balance.blockHeight == 0))
                                            {
                                                byte[] block_checksum = reader.ReadBytes((int)reader.ReadIxiVarUInt());

                                                balance.address = address;
                                                balance.balance = ixi_balance;
                                                balance.blockHeight = block_height;
                                                balance.blockChecksum = block_checksum;
                                                balance.verified = false;
                                            }

                                            if (waitingFor == code && waitForAddress != null && waitForAddress.SequenceEqual(address.addressWithChecksum))
                                            {
                                                blocked = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.updatePresence:
                        {
                            // Parse the data and update entries in the presence list
                            PresenceList.updateFromBytes(data, 0);
                        }
                        break;

                    case ProtocolMessageCode.blockHeaders3:
                        {
                            // Forward the block headers to the TIV handler
                            Node.tiv.receivedBlockHeaders3(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.pitData2:
                        {
                            Node.tiv.receivedPIT2(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.transactionData2:
                        {
                            Transaction tx = new Transaction(data, true, true);

                            if (endpoint.presenceAddress.type == 'M' || endpoint.presenceAddress.type == 'H')
                            {
                                PendingTransactions.increaseReceivedCount(tx.id, endpoint.presence.wallet);
                            }

                            if (Node.tiv.receivedNewTransaction(tx))
                            {
                                if (!Program.commands.stressRunning)
                                {
                                    Console.WriteLine("Received new transaction {0}", tx.getTxIdString());
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.bye:
                        CoreProtocolMessage.processBye(data, endpoint);
                        break;

                    default:
                        break;

                }
            }
            catch (Exception e)
            {
                Logging.error("Error parsing network message. Details: {0}", e);
            }

        }

    }
}
