using System;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using Infrastructure;
using NBitcoin;
using NBitcoin.Protocol;

namespace Network
{
	public class NATManager
	{
		private const int TIMEOUT = 5000;
		private const string MAPPING_DESC = "Node Lease (auto)";

		public IPAddress ExternalIPAddress { get; private set; }

//		#if DEBUG
		public IPAddress InternalIPAddress { get; private set; }
//		#else
//		private IPAddress InternalIPAddress { get; private set; }
//		#endif


		public bool DeviceFound { get; private set; }
		public bool HasError { get; private set; }
		public bool? Mapped { get; private set; }
		public bool? ExternalIPVerified { get; private set; }

		private NatDevice _NatDevice;
		private SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1);
		private int _ServerPort;

		public NATManager(int serverPort) //TODO: check internet connection is active
		{
			IPAddress[] PrivateIPs = NBitcoin.Utils.GetAllLocalIPv4();

			_ServerPort = serverPort;

			if (PrivateIPs.Count() == 0)
			{
				NodeServerTrace.Information("Warning, local addresses not found");
				InternalIPAddress = null;
			}
			else {
				InternalIPAddress = PrivateIPs.First();

				if (PrivateIPs.Count() > 1)
				{
					NodeServerTrace.Information("Warning, found " + PrivateIPs.Count() + " internal addresses");
				}
			}
		}

//		public bool HasConnectivity {
//			get {
//				return InternalIPAddress != null; // TODO: add conectivity check
//			}
//		}

		public async Task Init()
		{
			Mapped = null;
			ExternalIPVerified = null;

			await GetNatDeviceAsync();

			if (_NatDevice != null)
			{
				ExternalIPVerified = await VerifyExternalIP();

				if (ExternalIPVerified.Value)
				{
					Mapped = await EnsureMapping();
				}
			}
		}

		public async Task<NatDevice> GetNatDeviceAsync()
		{
			var nat = new NatDiscoverer();
			var cts = new CancellationTokenSource(TIMEOUT);

			if (_NatDevice != null)
			{
				return _NatDevice;
			}

			await _SemaphoreSlim.WaitAsync();
			NodeServerTrace.Information("NAT Device discovery started");

			return await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts).ContinueWith(t =>
			{
				_SemaphoreSlim.Release();
				DeviceFound = t.Status != TaskStatus.Faulted;

				if (!DeviceFound)
				{
					NodeServerTrace.Information("NAT Device not found");

					HasError = !(t.Exception.InnerException is NatDeviceNotFoundException);

					if (HasError)
					{
						NodeServerTrace.Error("NAT Device discovery", t.Exception);
					}
					return null;
				}
				else
				{
					_NatDevice = t.Result;
				}

				return _NatDevice;
			});
		}

		public async Task<bool> VerifyExternalIP()
		{
			NodeServerTrace.Information("VerifyExternalIP");

			var device = await GetNatDeviceAsync();

			return device == null ? false : await device.GetExternalIPAsync().ContinueWith(t =>
			{
				if (t.IsFaulted)
				{
					NodeServerTrace.Error("GetExternalIP", t.Exception);
					return false;
				}

				ExternalIPAddress = t.Result;

				if (ExternalIPAddress == null)
				{
					return false;
				}

				//try
				//{
				//	IPAddress ipAddress = ExternalTestingServicesHelper.GetExternalIPAsync().Result;

				//	bool match = ipAddress.Equals(ExternalIPAddress);

				//	Trace.Information("External IP " + (match ? "match" : "do not match"));

				//	return match;
				//}
				//catch (Exception e)
				//{
				//	Trace.Error("GetExternalIP", e);
				//	return false;
				//}
				return ExternalIPAddress.IsRoutable(false);
			});
		}

		public async Task<bool> EnsureMapping() {
			NodeServerTrace.Information("EnsureMapping");

			var device = await GetNatDeviceAsync();

			return device == null ? false : await device.GetSpecificMappingAsync(Protocol.Tcp, _ServerPort).ContinueWith(t =>
			{
				if (t.IsFaulted)
				{
					NodeServerTrace.Error("GetExternalIP", t.Exception);
					return false;
				}

				var mapping = t.Result;

				try
				{
					if (mapping != null && !mapping.PrivateIP.Equals(InternalIPAddress))
					{
						NodeServerTrace.Information($"existing mapping mismatch. got: {mapping.PrivateIP}, need: {InternalIPAddress}");

						_NatDevice.DeletePortMapAsync(mapping).Wait();
						mapping = null;
					}

					if (mapping == null)
					{
						NodeServerTrace.Information($"creaing mapping with IP: {InternalIPAddress}");

						_NatDevice.CreatePortMapAsync(
							new Mapping(
								Protocol.Tcp,
								InternalIPAddress,
								_ServerPort,
								_ServerPort,
								0, //TODO: session lifetime?
								MAPPING_DESC
							)
						).Wait();
					}

					IEnumerable<Mapping> exisingMappings = _NatDevice.GetAllMappingsAsync().Result;

                    return exisingMappings.Count(exisintMapping => exisintMapping.PublicPort == _ServerPort) == 1;
				}
				catch (Exception e)
				{
					NodeServerTrace.Error("Mapping", e);
					return false;
				}
			});
		}
	}
}
