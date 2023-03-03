using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockchainAssignment
{
    class Block
    {
        /* Block Variables */
        private DateTime timestamp; // Time of creation

        private int index, // Position of the block in the sequence of blocks
        difficulty = 4; // An arbitrary number of 0's to proceed a hash value

        public String prevHash, // A reference pointer to the previous block
            hash, // The current blocks "identity"
            merkleRoot,  // The merkle root of all transactions in the block
            minerAddress; // Public Key (Wallet Address) of the Miner

        public List<Transaction> transactionList; // List of transactions in this block
        
        // Proof-of-work
        public long nonce; // Number used once for Proof-of-Work and mining

        // Rewards
        public double reward; // Simple fixed reward established by "Coinbase"

        private const double TARGET_BLOCK_TIME = 60; // Target block time in seconds
        private const double DIFFICULTY_CHANGE_RATE = 0.25; // Difficulty change rate, 0.25 represents a 25% adjustment


        /* Genesis block constructor */
        public Block()
        {
            timestamp = DateTime.Now;
            index = 0;
            transactionList = new List<Transaction>();
            hash = Mine(1, null);
        }

        /* New Block constructor */
        public Block(Block lastBlock, List<Transaction> transactions, String minerAddress)
        {
            timestamp = DateTime.Now;

            index = lastBlock.index + 1;
            prevHash = lastBlock.hash;

            this.minerAddress = minerAddress; // The wallet to be credited the reward for the mining effort
            reward = 1.0; // Assign a simple fixed value reward
            transactions.Add(createRewardTransaction(transactions)); // Create and append the reward transaction
            transactionList = new List<Transaction>(transactions); // Assign provided transactions to the block

            merkleRoot = MerkleRoot(transactionList); // Calculate the merkle root of the blocks transactions

            // Set the difficulty based on the previous block's difficulty and time taken to mine the last block
            double expectedTime = TARGET_BLOCK_TIME * lastBlock.difficulty / difficulty;
            double actualTime = (timestamp - lastBlock.timestamp).TotalSeconds;
            if (actualTime < expectedTime - TARGET_BLOCK_TIME * DIFFICULTY_CHANGE_RATE)
            {
                difficulty++;
            }
            else if (actualTime > expectedTime + TARGET_BLOCK_TIME * DIFFICULTY_CHANGE_RATE)
            {
                difficulty--;
            }

            hash = Mine(1, null); // Conduct PoW to create a hash which meets the given difficulty requirement
            double blockTime = (timestamp - lastBlock.timestamp).TotalSeconds;
            Console.WriteLine("Block Time: {0} seconds", blockTime);

        }

        /* Hashes the entire Block object */
        public String CreateHash(int nonce, List<Transaction> transactions)
        {
            String hash = String.Empty;
            SHA256 hasher = SHA256Managed.Create();

            /* Concatenate all of the blocks properties including nonce as to generate a new hash on each call */
            StringBuilder input = new StringBuilder();
            input.Append(timestamp.ToString());
            input.Append(index);
            input.Append(prevHash);
            input.Append(nonce);

            if (transactions != null)
            {
                foreach (Transaction t in transactions)
                {
                    input.Append(t.senderAddress);
                    input.Append(t.recipientAddress);
                    input.Append(t.amount);
                    input.Append(t.fee);
                    input.Append(t.timestamp);
                }
            }

            input.Append(merkleRoot);

            /* Apply the hash function to the block as represented by the string "input" */
            Byte[] hashByte = hasher.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));

            /* Reformat to a string */
            foreach (byte x in hashByte)
                hash += String.Format("{0:x2}", x);

            return hash;
        }



        private long MineBlock(Block block, int numThreads)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            string hash = block.Mine(numThreads, null);
            stopwatch.Stop();
            Console.WriteLine("Block mined in {0} ms", stopwatch.ElapsedMilliseconds);
            return stopwatch.ElapsedMilliseconds;
        }


        // Create a Hash which satisfies the difficulty level required for PoW
        public string Mine(int numThreads, List<Transaction> transactions = null)
        {
            int nonce = 0;
            string hash = CreateHash(nonce, transactions);
            string re = new string('0', difficulty);

            var tasks = new Task<string>[numThreads];
            int stride = int.MaxValue / numThreads;

            for (int i = 0; i < numThreads; i++)
            {
                int start = i * stride;
                int end = start + stride;
                tasks[i] = Task.Run(() => FindValidHash(start, end, re, transactions));
            }

            Task.WaitAll(tasks);

            var hashSet = new HashSet<string>();

            foreach (var t in tasks)
            {
                if (t.Result != null && !hashSet.Contains(t.Result))
                {
                    hash = t.Result;
                    hashSet.Add(hash);
                }
            }

            return hash;
        }


        private string FindValidHash(int start, int end, string re, List<Transaction> transactions)
        {
            for (int nonce = start; nonce < end; nonce++)
            {
                string hash = CreateHash(nonce, transactions);
                if (hash.StartsWith(re))
                {
                    return hash;
                }
            }

            return null;
        }



        // Merkle Root Algorithm - Encodes transactions within a block into a single hash
        public static String MerkleRoot(List<Transaction> transactionList)
        {
            List<String> hashes = transactionList.Select(t => t.hash).ToList(); // Get a list of transaction hashes for "combining"
            
            // Handle Blocks with...
            if (hashes.Count == 0) // No transactions
            {
                return String.Empty;
            }
            if (hashes.Count == 1) // One transaction - hash with "self"
            {
                return HashCode.HashTools.combineHash(hashes[0], hashes[0]);
            }
            while (hashes.Count != 1) // Multiple transactions - Repeat until tree has been traversed
            {
                List<String> merkleLeaves = new List<String>(); // Keep track of current "level" of the tree

                for (int i=0; i<hashes.Count; i+=2) // Step over neighbouring pair combining each
                {
                    if (i == hashes.Count - 1)
                    {
                        merkleLeaves.Add(HashCode.HashTools.combineHash(hashes[i], hashes[i])); // Handle an odd number of leaves
                    }
                    else
                    {
                        merkleLeaves.Add(HashCode.HashTools.combineHash(hashes[i], hashes[i + 1])); // Hash neighbours leaves
                    }
                }
                hashes = merkleLeaves; // Update the working "layer"
            }
            return hashes[0]; // Return the root node
        }

        // Create reward for incentivising the mining of block
        public Transaction createRewardTransaction(List<Transaction> transactions)
        {
            double fees = transactions.Aggregate(0.0, (acc, t) => acc + t.fee); // Sum all transaction fees
            return new Transaction("Mine Rewards", minerAddress, (reward + fees), 0, ""); // Issue reward as a transaction in the new block
        }

        /* Concatenate all properties to output to the UI */
        public override string ToString()
        {
            return "[BLOCK START]"
                + "\nIndex: " + index
                + "\tTimestamp: " + timestamp
                + "\nPrevious Hash: " + prevHash
                + "\n-- PoW --"
                + "\nDifficulty Level: " + difficulty
                + "\nNonce: " + nonce
                + "\nHash: " + hash
                + "\n-- Rewards --"
                + "\nReward: " + reward
                + "\nMiners Address: " + minerAddress
                + "\n-- " + transactionList.Count + " Transactions --"
                +"\nMerkle Root: " + merkleRoot
                + "\n" + String.Join("\n", transactionList)
                + "\n[BLOCK END]";
        }
    }
}
