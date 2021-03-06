#if !NOSOCKET
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Filters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network.Serialization;
using Network;

namespace NBitcoin.Protocol
{
	public enum NodeState : int
	{
		Failed,
		Offline,
		Disconnecting,
		Connected,
		HandShaked
	}

	public class NodeDisconnectReason
	{
		public string Reason
		{
			get;
			set;
		}
		public Exception Exception
		{
			get;
			set;
		}
	}

	public class NodeRequirement
	{
		public ProtocolVersion? MinVersion
		{
			get;
			set;
		}
		public NodeServices RequiredServices
		{
			get;
			set;
		}

		public bool SupportSPV
		{
			get;
			set;
		}

		public virtual bool Check(VersionPayload version)
		{
			if(MinVersion != null)
			{
				if(version.Version < MinVersion.Value)
					return false;
			}
			if((RequiredServices & version.Services) != RequiredServices)
			{
				return false;
			}
			if(SupportSPV)
			{
				if(version.Version < ProtocolVersion.MEMPOOL_GD_VERSION)
					return false;
				if(ProtocolVersion.NO_BLOOM_VERSION <= version.Version && ((version.Services & NodeServices.NODE_BLOOM) == 0))
					return false;
			}
			return true;
		}
	}


	public delegate void NodeEventHandler(Node node);
	public delegate void NodeEventMessageIncoming(Node node, IncomingMessage message);
	public delegate void NodeStateEventHandler(Node node, NodeState oldState);
	public class Node : IDisposable
	{
		internal class SentMessage
		{
			public Object Payload;
			public TaskCompletionSource<bool> Completion;
			public Guid ActivityId;
		}
		public class NodeConnection
		{
			private readonly Node _Node;
			public Node Node
			{
				get
				{
					return _Node;
				}
			}
			readonly Socket _Socket;
			public Socket Socket
			{
				get
				{
					return _Socket;
				}
			}
			private readonly ManualResetEvent _Disconnected;
			public ManualResetEvent Disconnected
			{
				get
				{
					return _Disconnected;
				}
			}
			private readonly CancellationTokenSource _Cancel;
			public CancellationTokenSource Cancel
			{
				get
				{
					return _Cancel;
				}
			}
#if NOTRACESOURCE
			internal
#else
			public
#endif
 TraceCorrelation TraceCorrelation
			{
				get
				{
					return Node.TraceCorrelation;
				}
			}

			public NodeConnection(Node node, Socket socket)
			{
				_Node = node;
				_Socket = socket;
				_Disconnected = new ManualResetEvent(false);
				_Cancel = new CancellationTokenSource();
			}

			internal BlockingCollection<SentMessage> Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
			public void BeginListen()
			{
				new Thread(() =>
				{
					Thread.CurrentThread.Name = "Node Listener";

					SentMessage processing = null;
					Exception unhandledException = null;
					bool isVerbose = NodeServerTrace.Trace.Switch.ShouldTrace(TraceEventType.Verbose);
					ManualResetEvent ar = new ManualResetEvent(false);
					SocketAsyncEventArgs evt = new SocketAsyncEventArgs();
					evt.SocketFlags = SocketFlags.None;
					evt.Completed += (a, b) =>
					{
						Utils.SafeSet(ar);
					};
					try

					{
						foreach(var kv in Messages.GetConsumingEnumerable(Cancel.Token))
						{
							processing = kv;
							var payload = kv.Payload;

							if(isVerbose)
							{
								Trace.CorrelationManager.ActivityId = kv.ActivityId;
								if(kv.ActivityId != TraceCorrelation.Activity)
								{
									NodeServerTrace.Transfer(TraceCorrelation.Activity);
									Trace.CorrelationManager.ActivityId = TraceCorrelation.Activity;
								}
								NodeServerTrace.Information("Sending " + payload + " to " + _Node.RemoteSocketAddress);
							}

							var bytes = WireSerialization.Instance.Pack(payload);

							evt.SetBuffer(bytes, 0, bytes.Length);
							_Node.Counter.AddWritten(bytes.Length);
							ar.Reset();
							Socket.SendAsync(evt);
							WaitHandle.WaitAny(new WaitHandle[] { ar, Cancel.Token.WaitHandle }, -1);
							if(!Cancel.Token.IsCancellationRequested)
							{
								if(evt.SocketError != SocketError.Success)
									throw new SocketException((int)evt.SocketError);
								processing.Completion.SetResult(true);
								processing = null;
							}
						}
					}
					catch(OperationCanceledException)
					{
					}
					catch(Exception ex)
					{
						unhandledException = ex;
					}
					finally
					{
						evt.Dispose();
						ar.Dispose();
					}

					if(processing != null)
						Messages.Add(processing);

					foreach(var pending in Messages)
					{
						if(isVerbose)
						{
							Trace.CorrelationManager.ActivityId = pending.ActivityId;
							if(pending != processing && pending.ActivityId != TraceCorrelation.Activity)
								NodeServerTrace.Transfer(TraceCorrelation.Activity);
							Trace.CorrelationManager.ActivityId = TraceCorrelation.Activity;
							NodeServerTrace.Verbose("The connection cancelled before the message was sent");
						}
						pending.Completion.SetException(new OperationCanceledException("The peer has been disconnected"));
					}
					Messages = new BlockingCollection<SentMessage>(new ConcurrentQueue<SentMessage>());
					NodeServerTrace.Information("Stop sending");
					Cleanup(unhandledException);
				}).Start();
				new Thread(() =>
				{
					_ListenerThreadId = Thread.CurrentThread.ManagedThreadId;
					using(TraceCorrelation.Open(false))
					{
						NodeServerTrace.Information("Listening");
						Exception unhandledException = null;
						//byte[] buffer = _Node._ReuseBuffer ? new byte[1024 * 1024] : null;
						try
						{
							var stream = new NetworkStream(Socket, false);
							while(!Cancel.Token.IsCancellationRequested)
							{
								//PerformanceCounter counter;

								var payload = WireSerialization.Instance.Unpack(stream);

								//if(NodeServerTrace.Trace.Switch.ShouldTrace(TraceEventType.Verbose))
                                NodeServerTrace.Verbose("Receiving " + payload + " from " + _Node.RemoteSocketAddress);

								Node.LastSeen = DateTimeOffset.UtcNow;
								//Node.Counter.Add(counter);
								Node.OnMessageReceived(new IncomingMessage(payload)
								{
									Socket = Socket,
									//Length = counter.ReadenBytes,
									Node = Node
								});
							}
						}
						catch(OperationCanceledException)
						{
						}
						catch(Exception ex)
						{
							unhandledException = ex;
						}
						NodeServerTrace.Information("Stop listening");
						Cleanup(unhandledException);
					}
				}).Start();
			}			

			int _CleaningUp;
			public int _ListenerThreadId;
			private void Cleanup(Exception unhandledException)
			{
				if(Interlocked.CompareExchange(ref _CleaningUp, 1, 0) == 1)
					return;
				if(!Cancel.IsCancellationRequested)
				{
					Node.State = NodeState.Failed;
					NodeServerTrace.Error("Connection to server stopped unexpectedly", unhandledException);
					Node.DisconnectReason = new NodeDisconnectReason()
					{
						Reason = "Unexpected exception while connecting to socket",
						Exception = unhandledException
					};
				}

				if(Node.State != NodeState.Failed)
					Node.State = NodeState.Offline;

				_Cancel.Cancel();
				Utils.SafeCloseSocket(Socket);
				_Disconnected.Set(); //Set before behavior detach to prevent deadlock
				foreach(var behavior in _Node.Behaviors)
				{
					try
					{
						behavior.Detach();
					}
					catch(Exception ex)
					{
						NodeServerTrace.Error("Error while detaching behavior " + behavior.GetType().FullName, ex);
					}
				}
			}

		}

		public DateTimeOffset ConnectedAt
		{
			get;
			private set;
		}

		volatile NodeState _State = NodeState.Offline;
		public NodeState State
		{
			get
			{
				return _State;
			}
			private set
			{
				TraceCorrelation.LogInside(() => NodeServerTrace.Information("State changed from " + _State + " to " + value));
				var previous = _State;
				_State = value;
				if(previous != _State)
				{
					OnStateChanged(previous);
					if(value == NodeState.Failed || value == NodeState.Offline)
					{
						TraceCorrelation.LogInside(() => NodeServerTrace.Trace.TraceEvent(TraceEventType.Stop, 0, "Communication closed"));
						OnDisconnected();
					}
				}
			}
		}

		public event NodeStateEventHandler StateChanged;
		private void OnStateChanged(NodeState previous)
		{
			var stateChanged = StateChanged;
			if(stateChanged != null)
			{
				foreach(var handler in stateChanged.GetInvocationList().Cast<NodeStateEventHandler>())
				{
					try
					{
						handler.DynamicInvoke(this, previous);
					}
					catch(TargetInvocationException ex)
					{
						TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while StateChanged event raised", ex.InnerException));
					}
				}
			}
		}

		private readonly NodeFiltersCollection _Filters = new NodeFiltersCollection();
		public NodeFiltersCollection Filters
		{
			get
			{
				return _Filters;
			}
		}

		public event NodeEventMessageIncoming MessageReceived;
		protected void OnMessageReceived(IncomingMessage message)
		{
			message.IfPayloadIs<VersionPayload>(version =>
			{
				if (State == NodeState.HandShaked)
				{
					if (message.Node.Version >= ProtocolVersion.REJECT_VERSION)
						message.Node.SendMessageAsync(new RejectPayload()
						{
							Code = RejectCode.DUPLICATE
						});
				}
			});
			//if(version != null)
			//{
			//	if((version.Services & NodeServices.NODE_WITNESS) != 0)
			//		_SupportedTransactionOptions |= TransactionOptions.Witness;
			//}
			//var havewitness = message.Message.Payload as HaveWitnessPayload;
			//if(havewitness != null)
			//	_SupportedTransactionOptions |= TransactionOptions.Witness;

			var last = new ActionFilter((m, n) =>
			{
				MessageProducer.PushMessage(m);
				var messageReceived = MessageReceived;

				if(messageReceived != null)
				{
					foreach(var handler in messageReceived.GetInvocationList().Cast<NodeEventMessageIncoming>())
					{
						try
						{
							handler.DynamicInvoke(this, m);
						}
						catch(TargetInvocationException ex)
						{
							TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while OnMessageReceived event raised", ex.InnerException), false);
						}
					}
				}
			});

			var enumerator = Filters.Concat(new[] { last }).GetEnumerator();
			FireFilters(enumerator, message);
		}


		private void OnSendingMessage(Object payload, Action final)
		{
			var enumerator = Filters.Concat(new[] { new ActionFilter(null, (n, p, a) => final()) }).GetEnumerator();
			FireFilters(enumerator, payload);
		}

		private void FireFilters(IEnumerator<INodeFilter> enumerator, Object payload)
		{
			if(enumerator.MoveNext())
			{
				var filter = enumerator.Current;
				try
				{
					filter.OnSendingMessage(this, payload, () => FireFilters(enumerator, payload));
				}
				catch(Exception ex)
				{
					TraceCorrelation.LogInside(() => NodeServerTrace.Error("Unhandled exception raised by a node filter (OnSendingMessage)", ex.InnerException), false);
				}
			}
		}


		private void FireFilters(IEnumerator<INodeFilter> enumerator, IncomingMessage message)
		{
			if(enumerator.MoveNext())
			{
				var filter = enumerator.Current;
				try
				{
					filter.OnReceivingMessage(message, () => FireFilters(enumerator, message));
				}
				catch(Exception ex)
				{
					TraceCorrelation.LogInside(() => NodeServerTrace.Error("Unhandled exception raised by a node filter (OnReceivingMessage)", ex.InnerException), false);
				}
			}
		}

		public event NodeEventHandler Disconnected;
		private void OnDisconnected()
		{
			var disconnected = Disconnected;
			if(disconnected != null)
			{
				foreach(var handler in disconnected.GetInvocationList().Cast<NodeEventHandler>())
				{
					try
					{
						handler.DynamicInvoke(this);
					}
					catch(TargetInvocationException ex)
					{
						TraceCorrelation.LogInside(() => NodeServerTrace.Error("Error while Disconnected event raised", ex.InnerException));
					}
				}
			}
		}


		internal readonly NodeConnection _Connection;



		/// <summary>
		/// Connect to a random node on the network
		/// </summary>
		/// <param name="network">The network to connect to</param>
		/// <param name="addrman">The addrman used for finding peers</param>
		/// <param name="parameters">The parameters used by the found node</param>
		/// <param name="connectedAddresses">The already connected addresses, the new address will be select outside of existing groups</param>
		/// <returns></returns>
		public static Node Connect(NetworkInfo network, AddressManager addrman, NodeConnectionParameters parameters = null, IPAddress[] connectedAddresses = null)
		{
			parameters = parameters ?? new NodeConnectionParameters();
			AddressManagerBehavior.SetAddrman(parameters, addrman);
			return Connect(network, parameters, connectedAddresses);
		}

		/// <summary>
		/// Connect to a random node on the network
		/// </summary>
		/// <param name="network">The network to connect to</param>
		/// <param name="parameters">The parameters used by the found node, use AddressManagerBehavior.GetAddrman for finding peers</param>
		/// <param name="connectedAddresses">The already connected addresses, the new address will be select outside of existing groups</param>
		/// <returns></returns>
		public static Node Connect(NetworkInfo network, NodeConnectionParameters parameters = null, IPAddress[] connectedAddresses = null)
		{
			connectedAddresses = connectedAddresses ?? new IPAddress[0];
			parameters = parameters ?? new NodeConnectionParameters();
			var addrman = AddressManagerBehavior.GetAddrman(parameters) ?? new AddressManager();
			DateTimeOffset start = DateTimeOffset.UtcNow;
			while(true)
			{
				parameters.ConnectCancellation.ThrowIfCancellationRequested();
				if(addrman.Count == 0 || DateTimeOffset.UtcNow - start > TimeSpan.FromSeconds(10))
				{
					addrman.DiscoverPeers(network, parameters);
					start = DateTimeOffset.UtcNow;
				}
				NetworkAddress addr = null;
				while(true)
				{
					addr = addrman.Select();
					if(addr == null)
						break;
					if(!addr.Endpoint.Address.IsValid())
						continue;
					var groupExist = connectedAddresses.Any(a => a.GetGroup().SequenceEqual(addr.Endpoint.Address.GetGroup()));
					if(groupExist)
						continue;
					break;
				}
				if(addr == null)
					continue;
				try
				{
					var timeout = new CancellationTokenSource(5000);
					var param2 = parameters.Clone();
					param2.ConnectCancellation = CancellationTokenSource.CreateLinkedTokenSource(parameters.ConnectCancellation, timeout.Token).Token;
					var node = Node.Connect(network, addr.Endpoint, param2);
					return node;
				}
				catch(OperationCanceledException ex)
				{
					if(ex.CancellationToken == parameters.ConnectCancellation)
						throw;
				}
				catch(SocketException)
				{
					parameters.ConnectCancellation.WaitHandle.WaitOne(500);
				}
			}
		}

		/// <summary>
		/// Connect to the node of this machine
		/// </summary>
		/// <param name="network"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static Node ConnectToLocal(NetworkInfo network,
								NodeConnectionParameters parameters)
		{
			return Connect(network, Utils.ParseIpEndpoint("localhost", network.DefaultPort), parameters);
		}

		public static Node ConnectToLocal(NetworkInfo network,
								ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION,
								bool isRelay = true,
								CancellationToken cancellation = default(CancellationToken))
		{
			return ConnectToLocal(network, new NodeConnectionParameters()
			{
				ConnectCancellation = cancellation,
				IsRelay = isRelay,
				Version = myVersion
			});
		}

		public static Node Connect(NetworkInfo network,
								 string endpoint, NodeConnectionParameters parameters)
		{
			return Connect(network, Utils.ParseIpEndpoint(endpoint, network.DefaultPort), parameters);
		}

		public static Node Connect(NetworkInfo network,
								 string endpoint,
								 ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION,
								bool isRelay = true,
								CancellationToken cancellation = default(CancellationToken))
		{
			return Connect(network, Utils.ParseIpEndpoint(endpoint, network.DefaultPort), myVersion, isRelay, cancellation);
		}

		public static Node Connect(NetworkInfo network,
							 NetworkAddress endpoint,
							 NodeConnectionParameters parameters)
		{
			return new Node(endpoint, network, parameters);
		}

		public static Node Connect(NetworkInfo network,
							 IPEndPoint endpoint,
							 NodeConnectionParameters parameters)
		{
			var peer = new NetworkAddress()
			{
				Time = DateTimeOffset.UtcNow,
				Endpoint = endpoint
			};

			return new Node(peer, network, parameters);
		}

		public static Node Connect(NetworkInfo network,
								 IPEndPoint endpoint,
								 ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION,
								bool isRelay = true,
								CancellationToken cancellation = default(CancellationToken))
		{
			return Connect(network, endpoint, new NodeConnectionParameters()
			{
				ConnectCancellation = cancellation,
				IsRelay = isRelay,
				Version = myVersion,
				Services = NodeServices.Nothing,
			});
		}

		internal Node(NetworkAddress peer, NetworkInfo network, NodeConnectionParameters parameters)
		{
			parameters = parameters ?? new NodeConnectionParameters();
			var addrman = AddressManagerBehavior.GetAddrman(parameters);
			Inbound = false;
			_Behaviors = new NodeBehaviorsCollection(this);
			_MyVersion = parameters.CreateVersion(peer.Endpoint, network);
			Network = network;
			_Peer = peer;
			LastSeen = peer.Time;

			var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
			socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

			_Connection = new NodeConnection(this, socket);
			socket.ReceiveBufferSize = parameters.ReceiveBufferSize;
			socket.SendBufferSize = parameters.SendBufferSize;
			using(TraceCorrelation.Open())
			{
				try
				{
					var completed = new ManualResetEvent(false);
					var args = new SocketAsyncEventArgs();
					args.RemoteEndPoint = peer.Endpoint;
					args.Completed += (s, a) =>
					{
						Utils.SafeSet(completed);
					};
					if(!socket.ConnectAsync(args))
						completed.Set();
					WaitHandle.WaitAny(new WaitHandle[] { completed, parameters.ConnectCancellation.WaitHandle });
					parameters.ConnectCancellation.ThrowIfCancellationRequested();
					if(args.SocketError != SocketError.Success)
						throw new SocketException((int)args.SocketError);
					var remoteEndpoint = (IPEndPoint)(socket.RemoteEndPoint ?? args.RemoteEndPoint);
					_RemoteSocketAddress = remoteEndpoint.Address;
					_RemoteSocketPort = remoteEndpoint.Port;
					State = NodeState.Connected;
					ConnectedAt = DateTimeOffset.UtcNow;
					NodeServerTrace.Information("Outbound connection successfull");
					if(addrman != null)
						addrman.Attempt(Peer);
				}
				catch(OperationCanceledException)
				{
					Utils.SafeCloseSocket(socket);
					NodeServerTrace.Information("Connection to node cancelled");
					State = NodeState.Offline;
					if(addrman != null)
						addrman.Attempt(Peer);
					throw;
				}
				catch(Exception ex)
				{
					Utils.SafeCloseSocket(socket);
					NodeServerTrace.Error("Error connecting to the remote endpoint ", ex);
					DisconnectReason = new NodeDisconnectReason()
					{
						Reason = "Unexpected exception while connecting to socket",
						Exception = ex
					};
					State = NodeState.Failed;
					if(addrman != null)
						addrman.Attempt(Peer);
					throw;
				}
				InitDefaultBehaviors(parameters);
				_Connection.BeginListen();
			}
		}
		internal Node(NetworkAddress peer, NetworkInfo network, NodeConnectionParameters parameters, Socket socket, VersionPayload peerVersion)
		{
			_RemoteSocketAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
			_RemoteSocketPort = ((IPEndPoint)socket.RemoteEndPoint).Port;
			Inbound = true;
			_Behaviors = new NodeBehaviorsCollection(this);
			_MyVersion = parameters.CreateVersion(peer.Endpoint, network);
			Network = network;
			_Peer = peer;
			_Connection = new NodeConnection(this, socket);
			_PeerVersion = peerVersion;
			LastSeen = peer.Time;
			ConnectedAt = DateTimeOffset.UtcNow;
			TraceCorrelation.LogInside(() =>
			{
				NodeServerTrace.Information("Connected to advertised node " + _Peer.Endpoint);
				State = NodeState.Connected;
			});
			InitDefaultBehaviors(parameters);
			_Connection.BeginListen();
		}

		IPAddress _RemoteSocketAddress;
		public IPAddress RemoteSocketAddress
		{
			get
			{
				return _RemoteSocketAddress;
			}
		}

		int _RemoteSocketPort;
		public int RemoteSocketPort
		{
			get
			{
				return _RemoteSocketPort;
			}
		}

		public bool Inbound
		{
			get;
			private set;
		}

		bool _ReuseBuffer;
		private void InitDefaultBehaviors(NodeConnectionParameters parameters)
		{
			IsTrusted = parameters.IsTrusted != null ? parameters.IsTrusted.Value : Peer.Endpoint.Address.IsLocal();
		//	Advertize = parameters.Advertize;
		//	PreferredTransactionOptions = parameters.PreferredTransactionOptions;
			_ReuseBuffer = parameters.ReuseBuffer;

			_Behaviors.DelayAttach = true;
			foreach(var behavior in parameters.TemplateBehaviors)
			{
				_Behaviors.Add(behavior.Clone());
			}
			_Behaviors.DelayAttach = false;
		}

		private readonly NodeBehaviorsCollection _Behaviors;
		public NodeBehaviorsCollection Behaviors
		{
			get
			{
				return _Behaviors;
			}
		}

		private readonly NetworkAddress _Peer;
		public NetworkAddress Peer
		{
			get
			{
				return _Peer;
			}
		}

		public DateTimeOffset LastSeen
		{
			get;
			private set;
		}

		TraceCorrelation _TraceCorrelation = null;
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
#if NOTRACESOURCE
		internal
#else
		public
#endif
 TraceCorrelation TraceCorrelation
		{
			get
			{
				if(_TraceCorrelation == null)
				{
					_TraceCorrelation = new TraceCorrelation(NodeServerTrace.Trace, "Communication with " + Peer.Endpoint.ToString());
				}
				return _TraceCorrelation;
			}
		}

		/// <summary>
		/// Send a message to the peer asynchronously
		/// </summary>
		/// <param name="payload">The payload to send</param>
		/// <param name="System.OperationCanceledException">The node has been disconnected</param>
		public Task SendMessageAsync(Object payload)
		{
			if(payload == null)
				throw new ArgumentNullException("payload");
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
			if(!IsConnected)
			{
				completion.SetException(new OperationCanceledException("The peer has been disconnected"));
				return completion.Task;
			}
			var activity = Trace.CorrelationManager.ActivityId;
			Action final = () =>
			{
				_Connection.Messages.Add(new SentMessage()
				{
					Payload = payload,
					ActivityId = activity,
					Completion = completion
				});
			};
			OnSendingMessage(payload, final);
			return completion.Task;
		}



		/// <summary>
		/// Send a message to the peer synchronously
		/// </summary>
		/// <param name="payload">The payload to send</param>
		/// <exception cref="System.ArgumentNullException">Payload is null</exception>
		/// <param name="System.OperationCanceledException">The node has been disconnected, or the cancellation token has been set to canceled</param>
		public void SendMessage(Object payload, CancellationToken cancellation = default(CancellationToken))
		{
			try
			{
				SendMessageAsync(payload).Wait(cancellation);
			}
			catch(AggregateException aex)
			{
				ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
				throw;
			}
		}

		private PerformanceCounter _Counter;
		public PerformanceCounter Counter
		{
			get
			{
				if(_Counter == null)
					_Counter = new PerformanceCounter();
				return _Counter;
			}
		}

		/// <summary>
		/// The negociated protocol version (minimum of supported version between MyVersion and the PeerVersion)
		/// </summary>
		public ProtocolVersion Version
		{
			get
			{
				var peerVersion = PeerVersion == null ? MyVersion.Version : PeerVersion.Version;
				var myVersion = MyVersion.Version;
				var min = Math.Min((uint)peerVersion, (uint)myVersion);
				return (ProtocolVersion)min;
			}
		}

		public bool IsConnected
		{
			get
			{
				return State == NodeState.Connected || State == NodeState.HandShaked;
			}
		}

		private readonly MessageProducer<IncomingMessage> _MessageProducer = new MessageProducer<IncomingMessage>();
		public MessageProducer<IncomingMessage> MessageProducer
		{
			get
			{
				return _MessageProducer;
			}
		}

		public TPayload ReceiveMessage<TPayload>(TimeSpan timeout) 
			where TPayload : class
		{
			var source = new CancellationTokenSource();
			source.CancelAfter(timeout);
			return ReceiveMessage<TPayload>(source.Token);
		}

		public TPayload ReceiveMessage<TPayload>(CancellationToken cancellationToken = default(CancellationToken))
			where TPayload : class
		{
			using(var listener = new NodeListener(this))
			{
				return listener.ReceivePayload<TPayload>(cancellationToken);
			}
		}

		/// <summary>
		/// Send addr unsollicited message of the AddressFrom peer when passing to Handshaked state
		/// </summary>
		public bool Advertize
		{
			get;
			set;
		}

		private readonly VersionPayload _MyVersion;
		public VersionPayload MyVersion
		{
			get
			{
				return _MyVersion;
			}
		}

		VersionPayload _PeerVersion;
		public VersionPayload PeerVersion
		{
			get
			{
				return _PeerVersion;
			}
		}

		public void VersionHandshake(CancellationToken cancellationToken = default(CancellationToken))
		{
			VersionHandshake(null, cancellationToken);
		}
		public void VersionHandshake(NodeRequirement requirements, CancellationToken cancellationToken = default(CancellationToken))
		{
			requirements = requirements ?? new NodeRequirement();
			using (var listener = CreateListener()
				  .Where(p => p.IsPayloadTypeOf(typeof(VersionPayload), typeof(RejectPayload), typeof(VerAckPayload))))
			{

				SendMessageAsync(MyVersion);
				var payload = listener.ReceivePayload<Object>(cancellationToken);
				if(payload is RejectPayload)
				{
					throw new ProtocolException("Handshake rejected : " + ((RejectPayload)payload).Reason);
				}
				var version = (VersionPayload)payload;
				_PeerVersion = version;
				if(!version.AddressReceiver.Address.Equals(MyVersion.AddressFrom.Address))
				{
					NodeServerTrace.Warning("Different external address detected by the node " + version.AddressReceiver.Address + " instead of " + MyVersion.AddressFrom.Address);
				}
				if(version.Version < ProtocolVersion.MIN_PEER_PROTO_VERSION)
				{
					NodeServerTrace.Warning("Outdated version " + version.Version + " disconnecting");
					Disconnect("Outdated version");
					return;
				}

				if(!requirements.Check(version))
				{
					Disconnect("The peer does not support the required services requirement");
					return;
				}

				SendMessageAsync(new VerAckPayload());
				listener.ReceivePayload<VerAckPayload>(cancellationToken);
				State = NodeState.HandShaked;
				if(Advertize && MyVersion.AddressFrom.Address.IsRoutable(true))
				{
					SendMessageAsync(new AddrPayload(new NetworkAddress[] { 
						new NetworkAddress(MyVersion.AddressFrom)
						{
							Time = DateTimeOffset.UtcNow
						}
					}));
				}
			}
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="cancellation"></param>
		public void RespondToHandShake(CancellationToken cancellation = default(CancellationToken))
		{
			using(TraceCorrelation.Open())
			{
				using (var list = CreateListener().Where(m => m.IsPayloadTypeOf(typeof(VerAckPayload), typeof(RejectPayload))))
				{
					NodeServerTrace.Information("Responding to handshake");
					SendMessageAsync(MyVersion);
					var message = list.ReceiveMessage(cancellation);

					message.IfPayloadIs<RejectPayload>(reject =>
					{
						throw new ProtocolException("Version rejected " + reject.Code + " : " + reject.Reason);
					});
					SendMessageAsync(new VerAckPayload());
					State = NodeState.HandShaked;
				}
			}
		}

		public void Disconnect()
		{
			Disconnect(null, null);
		}

		int _Disconnecting;

		public void Disconnect(string reason, Exception exception = null)
		{
			DisconnectAsync(reason, exception);
			AssertNoListeningThread();
			_Connection.Disconnected.WaitOne();
		}

		private void AssertNoListeningThread()
		{
			if(_Connection._ListenerThreadId == Thread.CurrentThread.ManagedThreadId)
				throw new InvalidOperationException("Using Disconnect on this thread would result in a deadlock, use DisconnectAsync instead");
		}
		public void DisconnectAsync()
		{
			DisconnectAsync(null, null);
		}
		public void DisconnectAsync(string reason, Exception exception = null)
		{
			if(!IsConnected)
				return;
			if(Interlocked.CompareExchange(ref _Disconnecting, 1, 0) == 1)
				return;
			using(TraceCorrelation.Open())
			{
				NodeServerTrace.Information("Disconnection request " + reason);
				State = NodeState.Disconnecting;
				_Connection.Cancel.Cancel();
				if(DisconnectReason == null)
					DisconnectReason = new NodeDisconnectReason()
					{
						Reason = reason,
						Exception = exception
					};
			}
		}


		public NodeDisconnectReason DisconnectReason
		{
			get;
			private set;
		}

		public override string ToString()
		{
			return String.Format("{0} ({1})", State, Peer.Endpoint);
		}

		private Socket Socket
		{
			get
			{
				return _Connection.Socket;
			}
		}

		internal TimeSpan PollHeaderDelay = TimeSpan.FromMinutes(1.0);


		/// <summary>
		/// Will verify proof of work during chain operations
		/// </summary>
		public bool IsTrusted
		{
			get;
			set;
		}


		/// <summary>
		/// Create a listener that will queue messages until diposed
		/// </summary>
		/// <returns>The listener</returns>
		/// <exception cref="System.InvalidOperationException">Thrown if used on the listener's thread, as it would result in a deadlock</exception>
		public NodeListener CreateListener()
		{
			AssertNoListeningThread();
			return new NodeListener(this);
		}


		private void AssertState(NodeState nodeState, CancellationToken cancellationToken = default(CancellationToken))
		{
			if(nodeState == NodeState.HandShaked && State == NodeState.Connected)
				this.VersionHandshake(cancellationToken);
			if(nodeState != State)
				throw new InvalidOperationException("Invalid Node state, needed=" + nodeState + ", current= " + State);
		}


		public NetworkInfo Network
		{
			get;
			set;
		}

		#region IDisposable Members

		public void Dispose()
		{
			Disconnect("Node disposed");
		}

		#endregion

		/// <summary>
		/// Emit a ping and wait the pong
		/// </summary>
		/// <param name="cancellation"></param>
		/// <returns>Latency</returns>
		public TimeSpan PingPong(CancellationToken cancellation = default(CancellationToken))
		{
			using(var listener = CreateListener().OfType<PongPayload>())
			{
				var ping = new PingPayload()
				{
					Nonce = RandomUtils.GetUInt64()
				};
				var before = DateTimeOffset.UtcNow;
				SendMessageAsync(ping);

				while(listener.ReceivePayload<PongPayload>(cancellation).Nonce != ping.Nonce)
				{
				}
				var after = DateTimeOffset.UtcNow;
				return after - before;
			}
		}
	}
}
#endif