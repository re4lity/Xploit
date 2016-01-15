﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using XPloit.Core.Sockets.Proxy;

namespace XPloit.Core.Sockets
{
    public class TcpForwarder : Job.IJobable
    {
        class State
        {
            public bool IsSend;
            public Socket SourceSocket;
            public Socket DestinationSocket;
            public byte[] Buffer;

            public State(bool isSend, Socket source, Socket destination)
            {
                IsSend = isSend;
                SourceSocket = source;
                DestinationSocket = destination;
                Buffer = new byte[8192];
            }
        }

        List<TcpForwarder> socks = new List<TcpForwarder>();

        public delegate void delDataFilter(byte[] data, int index, int length);
        public delegate void delSocketEvent(object sender, EndPoint endPoint);

        delDataFilter _OnSend = null, _OnReceive;
        Socket MainSocket = null;
        IPEndPoint ProxySocket = null;
        byte bSocketType = 0;
        string _User, _Password;

        public event EventHandler OnDisposed;
        public event delSocketEvent OnConnect;
        public event delSocketEvent OnEror;

        /// <summary>
        /// Filter for send
        /// </summary>
        public delDataFilter FilterSend { get { return _OnSend; } set { _OnSend = value; } }
        /// <summary>
        /// Filter for receive
        /// </summary>
        public delDataFilter FilterReceive { get { return _OnReceive; } set { _OnReceive = value; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="proxySocket">ProxySocket</param>
        /// <param name="socketType">Socket Type</param>
        /// <param name="user">User</param>
        /// <param name="password">Password</param>
        public TcpForwarder(IPEndPoint proxySocket, byte socketType, string user, string password) :
            this(proxySocket, socketType, false, user, password)
        { }
        TcpForwarder(IPEndPoint proxySocket, byte socketType, bool thisIsSocks, string user, string password)
        {
            _User = user;
            _Password = password;

            if ((socketType == 4 || socketType == 5) && proxySocket != null)
            {
                ProxySocket = proxySocket;
                bSocketType = socketType;

                if (thisIsSocks)
                {
                    MainSocket = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        ProxyEndPoint = proxySocket,
                        ProxyType = socketType == 4 ? ProxyTypes.Socks4 : ProxyTypes.Socks5,
                        ProxyUser = string.IsNullOrEmpty(user) ? null : user,
                        ProxyPass = string.IsNullOrEmpty(password) ? null : password
                    };
                    return;
                }
            }

            MainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Start Forwarding
        /// </summary>
        /// <param name="local">IPEndPoint local</param>
        /// <param name="remote">IPEndPoint remote</param>
        public void Start(IPEndPoint local, IPEndPoint remote)
        {
            MainSocket.Bind(local);
            MainSocket.Listen(10);

            Thread th = new Thread(new ParameterizedThreadStart(thStart));
            th.IsBackground = true;
            th.Name = "TcpForwarder";
            th.Start(remote);
        }

        void thStart(object sender)
        {
            IPEndPoint remote = (IPEndPoint)sender;

            while (!IsDisposed)
            {
                try
                {
                    Socket source = MainSocket.Accept();

                    if (OnConnect != null)
                        OnConnect(this, source.RemoteEndPoint);

                    TcpForwarder destination = new TcpForwarder(ProxySocket, bSocketType, true, _User, _Password);
                    socks.Add(destination);

                    State state = new State(true, source, destination.MainSocket);
                    destination.Connect(remote, source);
                    source.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
                }
                catch
                {
                    break;
                }
            }
        }
        void Connect(EndPoint remoteEndpoint, Socket destination)
        {
            State state = new State(false, MainSocket, destination);

            try
            {
                if (MainSocket is ProxySocket) ((ProxySocket)MainSocket).ConnectSocks(remoteEndpoint);
                else MainSocket.Connect(remoteEndpoint);

                MainSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, OnDataReceive, state);
            }
            catch
            {
                if (OnConnect != null)
                    OnConnect(this, remoteEndpoint);
            }
        }

        /// <summary>
        /// Free resources
        /// </summary>
        public override void OnDispose()
        {
            bool disposed = false;
            try
            {
                if (MainSocket != null)
                {
                    MainSocket.Dispose();
                    MainSocket = null;
                    disposed = true;
                }
            }
            catch { }

            try
            {
                lock (socks)
                {
                    foreach (TcpForwarder tcp in socks) tcp.Dispose();
                }
            }
            catch { }

            if (disposed && OnDisposed != null) OnDisposed(this, EventArgs.Empty);

            base.OnDispose();
        }

        void OnDataReceive(IAsyncResult result)
        {
            State state = (State)result.AsyncState;
            try
            {
                int bytesRead = state.SourceSocket.EndReceive(result);
                if (bytesRead > 0)
                {
                    if (!state.IsSend)
                    {
                        if (_OnSend != null)
                        {
                            _OnSend(state.Buffer, 0, bytesRead);
                        }
                    }
                    else
                    {
                        if (_OnReceive != null)
                        {
                            _OnReceive(state.Buffer, 0, bytesRead);
                        }
                    }

                    state.DestinationSocket.Send(state.Buffer, bytesRead, SocketFlags.None);
                }

                state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
            }
            catch
            {
                state.DestinationSocket.Close();
                state.SourceSocket.Close();
            }
        }
    }
}