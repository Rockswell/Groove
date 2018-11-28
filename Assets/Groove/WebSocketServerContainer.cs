﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp.Server;
#endif

namespace Mirror.Groove
{
	public class WebSocketMessage
	{
		public int connectionId;
		public TransportEvent Type;
		public byte[] Data;
	}


	public class WebSocketServerContainer
	{
#if !UNITY_WEBGL || UNITY_EDITOR
		WebSocketServer WebsocketServer;

		private readonly bool UseSecureServer = false;
		private string PathToCertificate;
		private readonly string CertificatePassword = "FillMeOutPlease";

		public WebSocketServerContainer()
		{
			PathToCertificate = Application.dataPath + "/../certificate.pfx";
		}

		readonly Dictionary<int, MirrorWebSocketBehavior> WebsocketSessions = new Dictionary<int, MirrorWebSocketBehavior>();
		public int MaxConnections { get; private set; }

		private readonly Queue<WebSocketMessage> MessageQueue = new Queue<WebSocketMessage>();

		internal void AddMessage(WebSocketMessage webSocketMessage)
		{
			lock (MessageQueue)
			{
				MessageQueue.Enqueue(webSocketMessage);
			}
		}

		int connectionIdCounter = 1;

		public WebSocketMessage GetNextMessage()
		{
			lock (MessageQueue)
			{
				if (MessageQueue.Count > 0)
				{
					return MessageQueue.Dequeue();
				}
				return null;
			}
		}

		internal int NextId()
		{
			return Interlocked.Increment(ref connectionIdCounter);
		}

		internal void OnConnect(int connectionId, MirrorWebSocketBehavior socketBehavior)
		{
			lock (WebsocketSessions)
			{
				WebsocketSessions[connectionId] = socketBehavior;
			}
			var message = new WebSocketMessage { connectionId = connectionId, Type = TransportEvent.Connected };
			AddMessage(message);
		}

		internal void OnMessage(int connectionId, byte[] data)
		{
			var message = new WebSocketMessage { connectionId = connectionId, Type = TransportEvent.Data, Data = data };
			AddMessage(message);
		}

		internal void OnDisconnect(int connectionId)
		{
			lock (WebsocketSessions)
			{
				WebsocketSessions.Remove(connectionId);
			}
			var message = new WebSocketMessage { connectionId = connectionId, Type = TransportEvent.Disconnected };
			AddMessage(message);
		}

		public bool ServerActive { get { return WebsocketServer != null && WebsocketServer.IsListening; } }

		public bool RemoveConnectionId(int connectionId)
		{
			lock (WebsocketSessions)
			{
				MirrorWebSocketBehavior session;

				if (WebsocketSessions.TryGetValue(connectionId, out session))
				{
					session.Context.WebSocket.Close();
					WebsocketSessions.Remove(connectionId);
					return true;
				}
			}
			return false;
		}

		public void StopServer()
		{
			WebsocketServer.Stop();
			lock (WebsocketSessions)
			{
				WebsocketSessions.Clear();
			}
		}


		public void StartServer(string address, int port, int maxConnections)
		{
#if !UNITY_WEBGL || UNITY_EDITOR
			string scheme = UseSecureServer ? "wss://" : "ws://";
			if (string.IsNullOrEmpty(address))
			{
				address = "0.0.0.0";
			}

			Uri uri = new System.UriBuilder(scheme, address, port).Uri;
			if (Mirror.LogFilter.Debug)
			{
				Debug.Log("attempting to start WebSocket server on: " + uri.ToString());
			}
			MaxConnections = maxConnections;
			WebsocketServer = new WebSocketServer(uri.ToString());

			WebsocketServer.AddWebSocketService<MirrorWebSocketBehavior>("/game", (behaviour) =>
			{
				behaviour.Server = this;
				behaviour.connectionId = NextId();
			});

			if (UseSecureServer)
			{
				WebsocketServer.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(PathToCertificate, CertificatePassword);
			}
			WebsocketServer.Start();
#else
			Debug.Log("don't start the server on webgl please");
#endif
		}

		public bool GetConnectionInfo(int connectionId, out string address)
		{
			lock (WebsocketSessions)
			{
				MirrorWebSocketBehavior session;

				if (WebsocketSessions.TryGetValue(connectionId, out session))
				{
					address = session.Context.UserEndPoint.Address.ToString();
					return true;
				}
			}
			address = null;
			return false;
		}

		internal bool Send(int connectionId, byte[] data)
		{
			lock (WebsocketSessions)
			{
				MirrorWebSocketBehavior session;

				if (WebsocketSessions.TryGetValue(connectionId, out session))
				{
					session.SendData(data);
					return true;
				}
			}
			return false;
		}

#else
		public void StartServer(string address, int port, int maxConnections){
			Debug.LogError("can't start server in WebGL");
		}
#endif


	}
}