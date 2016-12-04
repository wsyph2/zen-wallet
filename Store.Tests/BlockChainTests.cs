using NUnit.Framework;
using System;
using Consensus;
using System.Collections.Generic;
using System.IO;
using BlockChain.Store;
using Store;
using System.Linq;

namespace BlockChain.Tests
{
	[TestFixture()]
	public class BlockChainTests
	{
		[Test()]
		public void ShoudRejectDuplicateBlock()
		{
			WithBlockChains(1, blockChain =>
			{
	//			Assert.That(blockChain[0].HandleNewBlock(Consensus.Tests.blk), Is.EqualTo(BlockChain.HandleNewBlockResult.AddedOrpan));
	//			Assert.That(blockChain[0].HandleNewBlock(Consensus.Tests.blk), Is.EqualTo(BlockChain.HandleNewBlockResult.Rejected));
			});
		}

		[Test()]
		public void CanStoreBlocks()
		{
			String dbName = "test-" + new Random().Next(0, 1000);

			List<Types.Block> blocks = new List<Types.Block>();

			for (int i = 0; i < 10; i++)
			{
				Types.Block newBlock = Util.GetBlock(null, new Random().Next(0, 1000));

				using (BlockChain blockChain = new BlockChain(dbName))
				{
					blockChain.HandleNewBlock(newBlock);
				}

				blocks.Add(newBlock);
			}


			MainBlockStore blockStore = new MainBlockStore();

			using (DBContext dbContext = new DBContext(dbName))
			{
				using (TransactionContext context = dbContext.GetTransactionContext())
				{
					foreach (Types.Block expected in blocks)
					{
						byte[] key = Merkle.blockHasher.Invoke(expected);

						Assert.IsTrue(blockStore.ContainsKey(context, key));

						Types.Block found = blockStore.Get(context, key).Value;

						Assert.AreEqual(expected, found, "match expected/found block");
					}
				}
			}

			Directory.Delete(dbName, true);
		}

		[Test()]
		public void CanStoreBlockDifficulty()
		{
			String dbName = "test-" + new Random().Next(0, 1000);

			var random = new Random();

			Double difficultyAgg = 0;

			var blocks = new List<Tuple<Types.Block, Double>>();

			Types.Block lastBlock = null;

			Console.WriteLine("Creating");

			for (int i = 0; i < 10; i++)
			{
				Double difficultyNew = random.Next(0, 1000);
				Types.Block newBlock = Util.GetBlock(lastBlock, difficultyNew);
				lastBlock = newBlock;

				using (BlockChain blockChain = new BlockChain(dbName))
				{
					Console.WriteLine("Handling block with difficulty: " + difficultyNew);

					blockChain.HandleNewBlock(newBlock);
				}

				difficultyAgg += difficultyNew;

				Console.WriteLine("Totoal difficulty: " + difficultyAgg);

				blocks.Add(new Tuple<Types.Block, Double>(newBlock, difficultyAgg));
			}

			Console.WriteLine("Checking");

			using (DBContext dbContext = new DBContext(dbName))
			{
				var BlockDifficultyTable = new BlockDifficultyTable();

				using (TransactionContext context = dbContext.GetTransactionContext())
				{
					foreach (Tuple<Types.Block, Double> blockDifficultyPair in blocks)
					{
						byte[] key = Merkle.blockHasher.Invoke(blockDifficultyPair.Item1);
						Double expected = blockDifficultyPair.Item2;
						Double found = BlockDifficultyTable.Context(context)[key];

						Assert.AreEqual(expected, found, "match expected/found block difficulty");
						Console.WriteLine("check passed..");
					}
				}
			}

			Directory.Delete(dbName, true);
		}

		//[Test()]
		//public void CanHandleNewValidBlock()
		//{
		//	Types.Block newBlock = GetBlock();

		//	String dbName = "test-" + new Random().Next(0, 1000);

		//	using (BlockChain blockChain = new BlockChain(dbName))
		//	{
		//		blockChain.HandleNewValueBlock(newBlock/*, 1*/);
		//	}

		//	Directory.Delete(dbName, true);
		//}

		//copied from elsewhere
		private void WithBlockChains(int blockChains, Action<BlockChain[]> action)
		{
			var testBlockChains = new List<TestBlockChain>();

			for (int i = 0; i < blockChains; i++)
			{
				String dbName = "test-" + new Random().Next(0, 1000);
				testBlockChains.Add(new TestBlockChain(dbName));
			}

			action(testBlockChains.Select(t => t.BlockChain).ToArray());

			foreach (var testBlockChain in testBlockChains)
			{
				testBlockChain.Dispose();
			}
		}

       //copied from elsewhere
		private class TestBlockChain : IDisposable
		{
			private readonly String _DbName;
			public BlockChain BlockChain { get; private set; }

			public TestBlockChain(String dbName)
			{
				_DbName = dbName;
				BlockChain = new BlockChain(dbName);
			}

			public void Dispose()
			{
				BlockChain.Dispose();
				Directory.Delete(_DbName, true);
			}
		}
	}
}