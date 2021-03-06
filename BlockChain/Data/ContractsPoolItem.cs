namespace BlockChain.Data
{
	public class ContractsPoolItem
	{
		public byte[] Hash { get; set; }
		public byte[] CostFn { get; set; }
		public ulong KalapasPerBlock { get; set; }
		public int Refs { get; set; }
		public string Extracted { get; set; }
		public byte[] Serialized { get; set; }

		public static ContractsPoolItem FromACSItem(ACSItem acsItem)
		{
			return new ContractsPoolItem()
			{
				Hash = acsItem.Hash,
				CostFn = acsItem.CostFn,
				KalapasPerBlock = acsItem.KalapasPerBlock,
				Refs = 0,
                Extracted = acsItem.Extracted,
                Serialized = acsItem.Serialized,
			};
		}
	}
}
