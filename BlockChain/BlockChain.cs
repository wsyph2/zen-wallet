using System;
using Consensus;
using BlockChain.Store;
using Store;
using Infrastructure;
using System.Collections.Generic;
using Microsoft.FSharp.Collections;
using System.Linq;
using BlockChain.Data;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;

namespace BlockChain
{
	public class BlockChain : ResourceOwner
	{
		readonly TimeSpan OLD_TIP_TIME_SPAN = TimeSpan.FromMinutes(5);
		readonly DBContext _DBContext;

		public MemPool memPool { get; set; }
		public UTXOStore UTXOStore { get; set; }
		public BlockStore BlockStore { get; set; }
		public BlockNumberDifficulties BlockNumberDifficulties { get; set; }
		public ChainTip ChainTip { get; set; }
		public BlockTimestamps Timestamps { get; set; }
		public byte[] GenesisBlockHash { get; set; }

		public enum IsOrphanResult
		{
			NotOrphan,
			Orphan,
			Invalid,
		}

#if DEBUG
		// for unit tests
		public void WaitDbTxs()
		{
			
			_DBContext.Wait();
		}
#endif

		public BlockChain(string dbName, byte[] genesisBlockHash)
		{
			_DBContext = new DBContext(dbName);
			memPool = new MemPool();
			UTXOStore = new UTXOStore();
			BlockStore = new BlockStore();
			BlockNumberDifficulties = new BlockNumberDifficulties();
			ChainTip = new ChainTip();
			Timestamps = new BlockTimestamps();
			GenesisBlockHash = genesisBlockHash;

			using (var context = _DBContext.GetTransactionContext())
			{
				var chainTip = ChainTip.Context(context).Value;

				//TODO: check if makred as main?
				Tip = chainTip == null ? null : BlockStore.GetBlock(context, chainTip);
			}

			InitBlockTimestamps();

			var buffer = new BufferBlock<QueueAction>();
			QueueAction.Target = buffer;
			var consumer = ConsumeAsync(buffer);

			OwnResource(_DBContext);
		}

		async Task ConsumeAsync(ISourceBlock<QueueAction> source)
		{
			while (await source.OutputAvailableAsync())
			{
				QueueAction action = source.Receive();

				try
				{
					if (action is HandleBlockAction)
						HandleBlock(action as HandleBlockAction);
					else if (action is HandleOrphansOfTxAction)
						HandleOrphansOfTransaction(action as HandleOrphansOfTxAction);
				}
				catch (Exception e)
				{
					BlockChainTrace.Error("action consumer", e);
				}
			}
		}

		void HandleOrphansOfTransaction(HandleOrphansOfTxAction a)
		{
			using (var dbTx = _DBContext.GetTransactionContext())
			{
				lock (memPool)
				{
					memPool.OrphanTxPool.GetOrphansOf(a.TxHash).ToList().ForEach(t =>
					{
						TransactionValidation.PointedTransaction ptx;
						var tx = memPool.OrphanTxPool[t];

						switch (IsOrphanTx(dbTx, tx, out ptx))
						{
							case IsOrphanResult.Orphan:
								return;
							case IsOrphanResult.Invalid:
								BlockChainTrace.Information("invalid orphan tx removed from orphans");
								break;
							case IsOrphanResult.NotOrphan:
								if (!memPool.TxPool.ContainsInputs(tx))
								{
									if (IsValidTransaction(dbTx, ptx))
									{
										BlockChainTrace.Information("unorphaned tx added to mempool");
										memPool.TxPool.Add(t, ptx);
									}
									else
									{
										BlockChainTrace.Information("invalid orphan tx removed from orphans");
									}
								}
								else
								{
									BlockChainTrace.Information("double spent orphan tx removed from orphans");
								}
								break;
						}

						memPool.OrphanTxPool.RemoveDependencies(t);
					});
				}
			}
		}

		/// <summary>
		/// Handles a new transaction from network or wallet. 
		/// </summary>
		/// <returns><c>true</c>, if new transaction was acceped, <c>false</c> rejected.</returns>
		public bool HandleTransaction(Types.Transaction tx)
		{
			using (TransactionContext context = _DBContext.GetTransactionContext())
			{
				TransactionValidation.PointedTransaction ptx;
				var txHash = Merkle.transactionHasher.Invoke(tx);

				lock (memPool)
				{
					if (memPool.TxPool.Contains(txHash))
					{
						BlockChainTrace.Information("Tx already in mempool");
						return false;
					}

					if (BlockStore.TxStore.ContainsKey(context, txHash))
					{
						BlockChainTrace.Information("Tx already in store");
						return false;
					}

					if (memPool.TxPool.ContainsInputs(tx))
					{
						BlockChainTrace.Information("Mempool contains spending input");
						return false;
					}

					switch (IsOrphanTx(context, tx, out ptx))
					{
						case IsOrphanResult.Orphan:
							BlockChainTrace.Information("tx added as orphan");
							memPool.OrphanTxPool.Add(txHash, tx);
							return true;
						case IsOrphanResult.Invalid:
							BlockChainTrace.Information("tx contains invalid reference(s)");
							return false;
					}


					//TODO: 5. For each input, if the referenced transaction is coinbase, reject if it has fewer than COINBASE_MATURITY confirmations.
					//TODO: 7. Apply fee rules. If fails, reject
					//TODO: 8. Validate each input. If fails, reject

					byte[] contractHash;
					if (IsContractGeneratedTx(ptx, out contractHash) && !IsContractGeneratedTransactionValid(context, ptx, contractHash))
						return false;

					if (!IsValidTransaction(context, ptx))
					{
						BlockChainTrace.Information("invalid inputs");
						return false;
					}

					BlockChainTrace.Information("new tx added to mempool");
					memPool.TxPool.Add(txHash, ptx);
				}
				return true;
			}
		}

		/// <summary>
		/// Handles a block from network.
		/// </summary>
		/// <returns><c>true</c>, if new block was acceped, <c>false</c> rejected.</returns>
		public bool HandleBlock(Types.Block bk)
		{
			return HandleBlock(new HandleBlockAction(bk));
		}

		bool HandleBlock(HandleBlockAction a)
		{
			BlockVerificationHelper action = null;

			using (var dbTx = _DBContext.GetTransactionContext())
			{
				action = new BlockVerificationHelper(
					this,
					dbTx,
					a.BkHash,
					a.Bk,
					a.IsOrphan
				);
				
				switch (action.Result)
				{
					case BlockVerificationHelper.ResultEnum.AddedOrphan:
						dbTx.Commit();
						break;
					case BlockVerificationHelper.ResultEnum.Added:
						UpdateMempool(dbTx, action.ConfirmedTxs, action.UnfonfirmedTxs);
						break;
					case BlockVerificationHelper.ResultEnum.Rejected:
						return false;
				}

				foreach (var _bk in BlockStore.Orphans(dbTx, a.BkHash))
				{
					new HandleBlockAction(_bk.Key, _bk.Value, true).Publish();
				}
			}

			foreach (var _action in action.QueuedActions)
			{
				if (_action is MessageAction)
					(_action as MessageAction).Message.Publish();
				else
					_action.Publish();
			}

			return true;
		}

		void UpdateMempool(TransactionContext dbTx, HashDictionary<Types.Transaction> confirmedTxs, HashDictionary<Types.Transaction> unconfirmedTxs)
		{
			lock (memPool)
			{
				dbTx.Commit();

				var activeContracts = new ActiveContractSet().Keys(dbTx);
				activeContracts.AddRange(memPool.ContractPool.Keys);
				               
				EvictTxToMempool(dbTx, unconfirmedTxs);
				RemoveConfirmedTxFromMempool(dbTx, confirmedTxs);

				var utxos = new List<Tuple<Types.Outpoint, Types.Output>>();

				foreach (var item in new UTXOStore().All(dbTx, null, false))
				{
					byte[] txHash = new byte[item.Key.Length - 1];
					Array.Copy(item.Key, txHash, txHash.Length);
					var index = Convert.ToUInt32(item.Key[item.Key.Length - 1]);

					utxos.Add(new Tuple<Types.Outpoint, Types.Output>(new Types.Outpoint(txHash, index), item.Value));
				}

				memPool.ICTxPool.Purge(activeContracts, utxos);
				memPool.TxPool.MoveToICTxPool(activeContracts);
			}
		}

		void EvictTxToMempool(TransactionContext dbTx, HashDictionary<Types.Transaction> unconfirmedTxs)
		{
			foreach (var tx in unconfirmedTxs)
			{
				TransactionValidation.PointedTransaction ptx = null;

				switch (IsOrphanTx(dbTx, tx.Value, out ptx))
				{
					case IsOrphanResult.NotOrphan:
						if (IsValidTransaction(dbTx, ptx))
						{
							BlockChainTrace.Information("tx evicted to mempool");
							memPool.TxPool.Add(tx.Key, ptx);

							//foreach (var contractHash in GetContractsActivatedBy(ptx))
							//{
							//	memPool.ContractPool.AddRef(dbTx, tx.Key, activeContracts);
							//}
						}
						break;
					case IsOrphanResult.Orphan: // is a double-spend
						memPool.TxPool.RemoveDependencies(tx.Key);
						new NewTxMessage(tx.Key, ptx, TxStateEnum.Invalid).Publish();
						break;
				}

				new NewTxMessage(tx.Key, ptx, TxStateEnum.Unconfirmed).Publish();
			}
		}

		void RemoveConfirmedTxFromMempool(TransactionContext dbTx, HashDictionary<Types.Transaction> confirmedTxs)
		{
			var spentOutputs = new List<Types.Outpoint>(); //TODO sort - efficiency

			foreach (var tx in confirmedTxs.Values)
			{
				spentOutputs.AddRange(tx.inputs);
			}

			foreach (var tx in confirmedTxs)
			{
				// check in ICTxs as well?
				if (memPool.TxPool.Contains(tx.Key))
				{
					BlockChainTrace.Information("same tx removed from txpool");
					memPool.TxPool.Remove(tx.Key);
					memPool.ContractPool.Remove(tx.Key);
				}
				else
				{
					new HandleOrphansOfTxAction(tx.Key).Publish(); // assume tx is unseen. try to unorphan
				}

				// Make list of **keys** in txpool and ictxpool
				// for each key in list, check if Double Spent. Remove recursively.
				// RemoveIfDoubleSpent is recursive over all pools, then sends a RemoveRef to ContractPool
				memPool.TxPool.RemoveDoubleSpends(spentOutputs);
				memPool.ICTxPool.RemoveDoubleSpends(spentOutputs);

				new MessageAction(new NewTxMessage(tx.Key, TxStateEnum.Confirmed)).Publish();
			}
		}

		public IsOrphanResult IsOrphanTx(TransactionContext dbTx, Types.Transaction tx, out TransactionValidation.PointedTransaction ptx)
		{
			var outputs = new List<Types.Output>();

			ptx = null;

			foreach (Types.Outpoint input in tx.inputs)
			{
				byte[] output = new byte[input.txHash.Length + 1];
				input.txHash.CopyTo(output, 0);
				output[input.txHash.Length] = (byte)input.index;

				if (UTXOStore.ContainsKey(dbTx, output)) //TODO: refactor ContainsKey, byte[] usage
				{
					outputs.Add(UTXOStore.Get(dbTx, output).Value);
				}

				if (memPool.TxPool.Contains(input.txHash))
				{
					if (input.index < memPool.TxPool[input.txHash].outputs.Length)
					{
						outputs.Add(memPool.TxPool[input.txHash].outputs[(int)input.index]);
					}
					else
					{
						BlockChainTrace.Information("can't construct ptx");
						return IsOrphanResult.Invalid;
					}
				}
			}

			if (outputs.Count < tx.inputs.Count())
			{
				return IsOrphanResult.Orphan;
			}

			ptx = TransactionValidation.toPointedTransaction(
				tx,
				ListModule.OfSeq<Types.Output>(outputs)
			);

			return IsOrphanResult.NotOrphan;
		}

		public bool IsValidTransaction(TransactionContext dbTx, TransactionValidation.PointedTransaction ptx)
		{
			//For each input, if the referenced output transaction is coinbase (i.e.only 1 input, with hash = 0, n = -1), it must have at least COINBASE_MATURITY (100) confirmations; else reject.
			//Verify crypto signatures for each input; reject if any are bad


			//Using the referenced output transactions to get input values, check that each input value, as well as the sum, are in legal money range
			//Reject if the sum of input values < sum of output values


			//for (var i = 0; i < ptx.pInputs.Length; i++)
			//{
			//	if (!TransactionValidation.validateAtIndex(ptx, i))
			//		return false;
			//}

			return true;
		}

		public static bool IsContractGeneratedTx(TransactionValidation.PointedTransaction ptx, out byte[] contractHash)
		{
			contractHash = null;

			foreach (var input in ptx.pInputs)
			{
				if (input.Item2.@lock.IsContractLock)
				{
					if (contractHash == null)
						contractHash = ((Types.OutputLock.ContractLock)input.Item2.@lock).contractHash;

					else if (!contractHash.SequenceEqual(((Types.OutputLock.ContractLock)input.Item2.@lock).contractHash))
					{
						BlockChainTrace.Information("Unexpected contactHash");
						return false;
					}
				}
				else return false;
			}

			return contractHash != null;
		}


		// TODO replace with two functions:
		// IsContractActive(contractHash), which checks if the contract is in the ACS on disk or in the contractpool;
		// bool IsContractGeneratedTransactionValid(dbtx, contracthash, ptx), which raises an exception if called with a missing contract
		public static bool IsContractGeneratedTransactionValid(TransactionContext dbTx, TransactionValidation.PointedTransaction ptx, byte[] contractHash)
		{
			if (new ActiveContractSet().IsActive(dbTx, contractHash))
			{

				/// 
				/// 
				/// 
				/// 
				/// 
				/// TODO: cache

				var utxos = new List<Tuple<Types.Outpoint, Types.Output>>();

				foreach (var item in new UTXOStore().All(dbTx, null, false))
				{
					byte[] txHash = new byte[item.Key.Length - 1];
					Array.Copy(item.Key, txHash, txHash.Length);
					var index = Convert.ToUInt32(item.Key[item.Key.Length - 1]);

					utxos.Add(new Tuple<Types.Outpoint, Types.Output>(new Types.Outpoint(txHash, index), item.Value));
				}

				var args = new ContractArgs()
				{
					context = new Types.ContractContext(contractHash, new FSharpMap<Types.Outpoint, Types.Output>(utxos)),
					//		inputs = inputs,
					witnesses = new List<byte[]>(),
					outputs = ptx.outputs.ToList(),
					option = Types.ExtendedContract.NewContract(null)
				};

				/// 
				/// 
				/// 
				/// 
				/// 


				Types.Transaction tx;
				if (!ContractHelper.Execute(contractHash, out tx, args))
				{
					BlockChainTrace.Information("Contract execution failed");
					return false;
				}

				return TransactionValidation.unpoint(ptx).Equals(tx);
			}
			else
			{
				BlockChainTrace.Information("Contract not active");
				return false;
			}

		}

		public TransactionContext GetDBTransaction()
		{
			return _DBContext.GetTransactionContext();
		}

		public void InitBlockTimestamps()
		{
			if (Tip != null)
			{
				var timestamps = new List<long>();
				var itr = Tip == null ? null : Tip.Value;

				while (itr != null && timestamps.Count < BlockTimestamps.SIZE)
				{
					timestamps.Add(itr.header.timestamp);
					itr = itr.header.parent.Length == 0 ? null : GetBlock(itr.header.parent);
				}
				Timestamps.Init(timestamps.ToArray());
			}
		}

		public bool IsTipOld
		{
			get
			{
				return Tip == null ? true : DateTime.Now - DateTime.FromBinary(Tip.Value.header.timestamp) > OLD_TIP_TIME_SPAN;
			}
		}

		//TODO: refactor
		public Keyed<Types.Block> Tip { get; set; }

		public Types.Transaction GetTransaction(byte[] key) //TODO: make concurrent
		{
			if (memPool.TxPool.Contains(key))
			{
				return TransactionValidation.unpoint(memPool.TxPool[key]);
			}
			else
			{
				using (TransactionContext context = _DBContext.GetTransactionContext())
				{
					if (BlockStore.TxStore.ContainsKey(context, key))
					{
						return BlockStore.TxStore.Get(context, key).Value;
					}
				}
			}

			return null;
		}

		//TODO: should asset that the block came from main?
		public Types.Block GetBlock(byte[] key)
		{
			using (TransactionContext context = _DBContext.GetTransactionContext())
			{
				var bk = BlockStore.GetBlock(context, key);

				return bk == null ? null : bk.Value;
			}
		}

		// TODO: use linq, return enumerator, remove predicate
		public Dictionary<Keyed<Types.Transaction>, Types.Output> GetUTXOSet(Func<Types.Output, bool> predicate)
		{
			var outputs = new HashDictionary<Types.Output>();
			var values = new Dictionary<Keyed<Types.Transaction>, Types.Output>();

			using (TransactionContext context = _DBContext.GetTransactionContext())
			{
				foreach (var output in UTXOStore.All(context, predicate, true))
				{
					byte[] txHash = new byte[output.Key.Length - 1];
					Array.Copy(output.Key, txHash, txHash.Length);
					outputs[txHash] = output.Value;
				}

				foreach (var output in outputs)
				{
					var tx = BlockStore.TxStore.Get(context, output.Key);
					values[tx] = output.Value;
				}
			}

			return values;
		}

		////demo
		public Types.Block MineAllInMempool()
		{
			if (memPool.TxPool.Count == 0)
			{
				return null;
			}

			uint version = 1;
			string date = "2000-02-02";

		//	Merkle.Hashable x = new Merkle.Hashable ();
		//	x.
		//	var merkleRoot = Merkle.merkleRoot(Tip.Key,

			var nonce = new byte[10];

			new Random().NextBytes (nonce);

			var blockHeader = new Types.BlockHeader(
				version,
				Tip.Key,
				0,
				new byte[] { },
				new byte[] { },
				new byte[] { },
				ListModule.OfSeq<byte[]>(new List<byte[]>()),
				DateTime.Parse(date).ToBinary(),
				1,
				nonce
			);

			var newBlock = new Types.Block(blockHeader, ListModule.OfSeq<Types.Transaction>(memPool.TxPool.Select(
				t => TransactionValidation.unpoint(t.Value)))
          	);

			if (HandleBlock(newBlock))
			{
				return newBlock;
			}
			else 
			{
				BlockChainTrace.Information("*** error mining block ***");
				//	throw new Exception();
				return null;
			}
		}
	}
}
