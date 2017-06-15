﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using Consensus;
using ContractsDiscovery.Web.App_Data;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Wallet.core.Data;
using Zen.RPC;
using Zen.RPC.Common;
using static ContractExamples.Execution;

namespace ContractsDiscovery.Web.Controllers
{
    public class ContractInteractionController : Controller
    {
        string _address = WebConfigurationManager.AppSettings["node"];
		const byte OPCODE_BUY = 0x01;
		const byte OPCODE_COLLATERALIZE = 0x00;

		[HttpPost]
		public ActionResult PrepareAction()
		{
			var action = Request["Action"];
			var address = Request["ActiveContract.Address"];

			var contractInteraction = new ContractInteraction()
            {
                Action = action,
                Address = address
            };

			return View(contractInteraction);
		}

		public UInt64 ByteArrayToUInt64(byte[] data)
		{
			if (BitConverter.IsLittleEndian)
				Array.Reverse(data);

			return System.BitConverter.ToUInt64(data, 0);
		}

		public UInt64 ParseCollateralData(byte[] data)
		{
			try
			{
				return ByteArrayToUInt64(data.Skip(8).Take(15).ToArray());
			}
			catch
			{
				return 0;
			}
		}

        [HttpPost]
        public async Task<ActionResult> Action()
        {
            var action = Request["Action"];
            var args = new Dictionary<string, string>();
			byte opcode = default(byte);

			var address = Request["Address"];

			var contractHash = Convert.FromBase64String(address);

			string key = HttpServerUtility.UrlTokenEncode(contractHash);
			var file = $"{key}";

			string contractCode = null;
			var codeFile = Path.ChangeExtension(Path.Combine("db", "contracts", file), ".txt");
            if (System.IO.File.Exists(codeFile))
            {
                contractCode = System.IO.File.ReadAllText(codeFile);
            }

            var contractInteraction = new ContractInteraction()
			{
				Action = action,
				Address = new Address(contractHash, AddressType.Contract).ToString()
			};

            ContractMetadata contractMetadata = null;

			try
			{
				var _metadata = ContractExamples.Execution.metadata(contractCode);

				if (FSharpOption<ContractMetadata>.get_IsNone(_metadata))
				{
					contractInteraction.Message = "No metadata";
				}
                else
                {
                    contractMetadata = _metadata.Value;
                }
			}
            catch
			{
				contractInteraction.Message = "Error getting metadata";
				return View(contractInteraction);
			}

            if (contractMetadata.IsCallOption)
            {
                var callOptionParameters = 
                    (ContractExamples.Execution.ContractMetadata.CallOption)contractMetadata;
                switch (action)
                {
                    case "Collateralize":
                        args.Add("returnPubKeyAddress", Request["return-address"]);
                        opcode = OPCODE_COLLATERALIZE;
                        break;
                    case "Exercise":
                        var oracleData = GetOracleCommitmentData(callOptionParameters.Item.underlying, DateTime.Now.ToUniversalTime()).Result;
						
               //         args.Add("oracleOutpoint", );
				//		args.Add("auditPath", );


						contractInteraction.Data = oracleData.Proof;
                        return View(contractInteraction);
                        break;
                }
            }

            var argsMap = new FSharpMap<string, string>(args.Select(t => new Tuple<string, string>(t.Key, t.Value)));
            var result = await Client.Send<GetContractPointedOutputsResultPayload>(_address, new GetContractPointedOutputsPayload() { ContractHash = contractHash });
            var utxos = GetContractPointedOutputsResultPayload.Unpack(result.PointedOutputs);
            var data = ContractUtilities.DataGenerator.makeData(contractMetadata, utxos, opcode, argsMap);

            if (FSharpOption<string>.get_IsNone(data))
            {
                contractInteraction.Message = "No data";
            }
            else
            {
                contractInteraction.Data = data.Value;
            }
          
			return View(contractInteraction);
		}

        async Task<Zen.Services.Oracle.Common.CommitmentData> GetOracleCommitmentData(string underlying, DateTime time)
        {
            string oracleGetCommitmentDataService = WebConfigurationManager.AppSettings["oracleGetCommitmentDataService"];
            var uri = new Uri(string.Format(oracleGetCommitmentDataService, underlying));
            string remoteString = null;
            Zen.Services.Oracle.Common.CommitmentData result = null;

            try
            {
                var response = await new HttpClient().GetAsync(uri.AbsoluteUri).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    remoteString = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

			try
			{
				result = JsonConvert.DeserializeObject<Zen.Services.Oracle.Common.CommitmentData>(remoteString);
			}
			catch (Exception e)
			{
				return null;
			}

            return result;
        }
	}
}