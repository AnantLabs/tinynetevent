using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Diagnostics;

namespace OpenSource.Net
{
    public class NetEvent
    {
        internal class ReplacableQueue<T>
        {
            List<T> innerList;
            object syncRoot;
            long bytes;
            public delegate int lenhandler(T item);
            lenhandler len;

            internal static ReplacableQueue<T> Synchronized(ReplacableQueue<T> queue)
            {
                queue.syncRoot = new object();
                return queue;
            }

            internal ReplacableQueue(lenhandler h)
                : this()
            {
                len = h;
            }

            internal ReplacableQueue()
            {
                innerList = new List<T>();
                syncRoot = null;
                bytes = 0;
                len = null;
            }

            internal bool IsSynchronized
            {
                get { return syncRoot != null; }
            }

            internal object SyncRoot
            {
                get
                {
                    if (!IsSynchronized) throw new NotSupportedException();
                    return syncRoot;
                }
            }

            internal int Count
            {
                get { return innerList.Count; }
            }

            internal void Enqueue(T item)
            {
                if (IsSynchronized)
                {
                    lock (syncRoot)
                    {
                        enqueue(item);
                    }
                }
                else
                {
                    enqueue(item);
                }
            }

            void enqueue(T item)
            {
                if (len != null) bytes += len(item);

                innerList.Add(item);
            }

            internal T Dequeue()
            {
                if (IsSynchronized)
                {
                    lock (syncRoot)
                    {
                        return dequeue();
                    }
                }
                else
                {
                    return dequeue();
                }
            }

            T dequeue()
            {
                T item = innerList[0];
                innerList.RemoveAt(0);

                if (len != null) bytes -= len(item);

                return item;
            }

            internal T Peek()
            {
                if (IsSynchronized)
                {
                    lock (syncRoot)
                    {
                        return peek();
                    }
                }
                else
                {
                    return peek();
                }
            }

            T peek()
            {
                return innerList[0];
            }

            internal void Set(T item)
            {
                if (IsSynchronized)
                {
                    lock (syncRoot)
                    {
                        set(item);
                    }
                }
                else
                {
                    set(item);
                }
            }

            void set(T item)
            {
                if (len != null)
                {
                    bytes -= len(innerList[0]) - len(item);
                }

                innerList[0] = item;
            }

            internal long TotalBytes
            {
                get
                {
                    if (IsSynchronized)
                    {
                        Interlocked.Read(ref bytes);
                    }
                    return bytes;
                }
            }
        }

        internal sealed class Peer
        {
            internal ReplacableQueue<ArraySegment<byte>> _recvbuffers;
            internal TcpClient client;
            internal byte[] _recvdata;
            internal int _len;
            internal int _id;
            internal int _read;
            internal bool _check;
            internal DateTime _lastdisconnectcheck;

            internal Peer(TcpClient c)
                : this()
            {
                client = c;
                _id = client.Client.Handle.ToInt32();
            }

            internal Peer()
            {
                client = new TcpClient();
                _len = 0;
                _recvdata = new byte[_len];
                _recvbuffers = ReplacableQueue<ArraySegment<byte>>.Synchronized(new ReplacableQueue<ArraySegment<byte>>(delegate(ArraySegment<byte> item) { return item.Array.Length; }));
                _id = client.Client.Handle.ToInt32();
                _read = 0;
                _check = false;
                _lastdisconnectcheck = DateTime.MinValue;
            }

            internal void AddBuffer(byte[] buffer)
            {
                _recvbuffers.Enqueue(new ArraySegment<byte>(buffer));
            }

            internal long MemoryUsage
            {
                get
                {
                    return client.ReceiveBufferSize + client.SendBufferSize + _recvdata.Length + _recvbuffers.TotalBytes + sizeof(bool) + sizeof(int) * 3;
                }
            }

            internal bool Connected
            {
                get
                {
                    if (client == null) return false;
                    if (client.Client == null) return false;
                    try
                    {
                        return client.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            public override int GetHashCode()
            {
                return _id;
            }

            public override bool Equals(object obj)
            {
                if (obj is Peer)
                {
                    return _id == ((Peer)obj)._id;
                }
                return false;
            }
        }

        internal struct SendBuff
        {
            internal Peer peer;
            internal byte[] buff;
        }

        internal sealed class RWLock
        {
#if NETFX2
            ReaderWriterLock locker;
#else
            ReaderWriterLockSlim locker;
#endif

            internal RWLock()
            {
#if NETFX2
                locker = new ReaderWriterLock();
#else
                locker = new ReaderWriterLockSlim();
#endif
            }

            internal void AcquireReaderLock(int timeout)
            {
#if NETFX2
                locker.AcquireReaderLock(timeout);
#else
                locker.TryEnterReadLock(timeout);
#endif
            }

            internal void ReleaseReaderLock()
            {
#if NETFX2
                locker.ReleaseReaderLock();
#else
                locker.ExitReadLock();
#endif
            }

            internal void AcquireWriterLock(int timeout)
            {
#if NETFX2
                locker.AcquireWriterLock(timeout);
#else
                locker.TryEnterWriteLock(timeout);
#endif            
            }

            internal void ReleaseWriterLock()
            {
#if NETFX2
                locker.ReleaseWriterLock();
#else
                locker.ExitWriteLock();
#endif
            }
        }

        const long maxMemoryUsage = 100 * 1024 * 1024;
        const int recvBufferSize = 64 * 1024;
        readonly TimeSpan disconnectCheckSpan = new TimeSpan(0, 0, 0, 3, 0);
        delegate void f();
        TcpListener server;
        INetEvent handler;
        Queue op;
        Dictionary<int, Peer> peers;
        bool running;
        int count;

        RWLock locker;

        ReplacableQueue<SendBuff> sendbuffers;

        Peer GetPeer(int nID)
        {
            Peer client;

            locker.AcquireReaderLock(Timeout.Infinite);
            peers.TryGetValue((int)nID, out client);
            locker.ReleaseReaderLock();

            return client;
        }

        public void Close(int nID)
        {
            Peer c = GetPeer(nID);
            if (c == null) return;
            if (c.Connected)
            {
                try
                {
                    if (c.client.Client.Blocking)
                    {
                        c.client.Client.Shutdown(SocketShutdown.Both);
                        c.client.Client.Close();
                    }
                    c.client.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }
            }

            locker.AcquireWriterLock(Timeout.Infinite);
            peers.Remove((int)nID);
            locker.ReleaseWriterLock();

            handler.OnClose(nID);
        }

        public void Connect(string lpAddr, int nPort)
        {
            Connect(lpAddr, nPort, null);
        }

        public void Connect(string lpAddr, int nPort, object state)
        {
            f cd = delegate()
            {
                TcpClient c = new TcpClient();
                try
                {
                    c.BeginConnect(lpAddr, nPort, delegate(IAsyncResult ar)
                    {
                        try
                        {
                            c.EndConnect(ar);
                            c.LingerState = new LingerOption(false, 0);
                            c.NoDelay = true;

                            Peer p = new Peer(c);

                            f d = delegate()
                            {
                                if (state == null) handler.OnConnect((int)p._id);
                                handler.OnConnect((int)p._id, state);
                            };
                            op.Enqueue(d);

                            locker.AcquireWriterLock(Timeout.Infinite);
                            peers[p._id] = p;
                            locker.ReleaseWriterLock();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                            Debug.WriteLine(ex.StackTrace);
                            f d = delegate()
                            {
                                if (state == null) handler.OnConnectFailed();
                                handler.OnConnectFailed(state);
                            };
                            op.Enqueue(d);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                    if (state == null) handler.OnConnectFailed();
                    handler.OnConnectFailed(state);
                }
            };

            op.Enqueue(cd);
        }

        public void Create(INetEvent hander, int nPort, int nCount, bool encrypt)
        {
            handler = hander;
            server = new TcpListener(IPAddress.Any, nPort);
            server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            server.Server.LingerState = new LingerOption(false, 0);
            server.Server.NoDelay = true;
            server.Server.Blocking = false;

            op = Queue.Synchronized(new Queue(nCount));
            peers = new Dictionary<int, Peer>(nCount);
            count = nCount;
            locker = new RWLock();
            sendbuffers = ReplacableQueue<SendBuff>.Synchronized(new ReplacableQueue<SendBuff>(delegate(SendBuff item) { return item.buff.Length; }));

            ThreadPool.SetMaxThreads(100, 100);
            ThreadPool.SetMinThreads(10, 10);

            Thread t = new Thread(delegate()
            {
                while (running)
                {
                    if (peers.Count > 0)
                    {
                        Peer[] clients;
                        locker.AcquireReaderLock(Timeout.Infinite);
                        clients = new Peer[peers.Count];
                        peers.Values.CopyTo(clients, 0);
                        locker.ReleaseReaderLock();

                        for (int i = 0; i < clients.Length; ++i)
                        {
                            Peer c = clients[i];
                            if (!c.Connected) continue;

                            try
                            {
                                Socket s = c.client.Client;
                                if (s.Poll(0, SelectMode.SelectRead) && s.Available > 0)
                                {
                                    byte[] recvbuffer = new byte[recvBufferSize];
                                    if (!s.Connected) continue;
                                    if (c._check) continue;
                                    if (c._read > 0) continue;
                                    Interlocked.Increment(ref c._read);

                                    try
                                    {
                                        s.BeginReceive(recvbuffer, 0, recvBufferSize, SocketFlags.None, delegate(IAsyncResult ar)
                                        {
                                            try
                                            {
                                                if (s == null || !s.Connected) return;
                                                int readSize = s.EndReceive(ar);
                                                byte[] buf = new byte[readSize];
                                                Buffer.BlockCopy(recvbuffer, 0, buf, 0, readSize);
                                                c.AddBuffer(buf);
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine(ex.Message);
                                                Debug.WriteLine(ex.StackTrace);
                                                f d = delegate()
                                                {
                                                    Close((ushort)((ar.AsyncState as object[])[0] as Peer)._id);
                                                };
                                                op.Enqueue(d);
                                            }
                                            finally
                                            {
                                                Interlocked.Decrement(ref c._read);
                                            }
                                        }, null);
                                    }
                                    catch
                                    {
                                        Interlocked.Decrement(ref c._read);
                                        throw;
                                    }
                                }
                            }
                            catch (SocketException)
                            {
                                f d = delegate()
                                {
                                    Close((ushort)c._id);
                                };
                                op.Enqueue(d);
                            }
                            catch (ObjectDisposedException) { }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                Debug.WriteLine(ex.StackTrace);
                                f d = delegate()
                                {
                                    Close((ushort)c._id);
                                };
                                op.Enqueue(d);
                            }
                        }
                    }
                    Thread.CurrentThread.Join(1);
                }
            });

            Thread u = new Thread(delegate()
            {
                while (running)
                {
                    if (peers.Count > 0)
                    {
                        Peer[] clients;
                        locker.AcquireReaderLock(Timeout.Infinite);
                        clients = new Peer[peers.Count];
                        peers.Values.CopyTo(clients, 0);
                        locker.ReleaseReaderLock();

                        for (int i = 0; i < clients.Length; ++i)
                        {
                            Peer c = clients[i];
                            try
                            {
                                if (!c.Connected) continue;
                                if (c._recvbuffers.Count == 0) continue;
                                int copylen;
                                ArraySegment<byte> buffer;
                                byte[] data;
                                if (c._len <= 0)
                                {
                                    buffer = c._recvbuffers.Peek();
                                    data = new byte[sizeof(int)];
                                    if (buffer.Count < data.Length)
                                    {
                                        c._recvbuffers.Dequeue();
                                        continue;
                                    }
                                    Buffer.BlockCopy(buffer.Array, buffer.Offset, data, 0, data.Length);
                                    c._len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 0));
                                    if (c._len <= 0)
                                    {
                                        c._recvbuffers.Dequeue();
                                        continue;
                                    }
                                    c._recvdata = new byte[c._len];
                                    copylen = 0;
                                    if (buffer.Count > data.Length)
                                    {
                                        copylen = buffer.Count - data.Length;
                                        if (copylen > c._len)
                                        {
                                            copylen = c._len;
                                            Buffer.BlockCopy(buffer.Array, data.Length, c._recvdata, 0, copylen);
                                            Buffer.BlockCopy(buffer.Array, data.Length + copylen, buffer.Array, 0, buffer.Count - (data.Length + copylen));
                                            buffer = new ArraySegment<byte>(buffer.Array, 0, buffer.Count - (data.Length + copylen));
                                            c._recvbuffers.Set(buffer);
                                        }
                                        else
                                        {
                                            Buffer.BlockCopy(buffer.Array, data.Length, c._recvdata, 0, copylen);
                                            c._recvbuffers.Dequeue();
                                        }
                                    }
                                    c._len -= copylen;
                                }
                                while (c._len > 0 && c._recvbuffers.Count > 0)
                                {
                                    buffer = c._recvbuffers.Peek();
                                    if (buffer.Count <= c._len)
                                    {
                                        copylen = buffer.Count;
                                        Buffer.BlockCopy(buffer.Array, 0, c._recvdata, 0, copylen);
                                        c._recvbuffers.Dequeue();
                                    }
                                    else
                                    {
                                        copylen = c._len;
                                        Buffer.BlockCopy(buffer.Array, 0, c._recvdata, 0, copylen);
                                        Buffer.BlockCopy(buffer.Array, copylen, buffer.Array, 0, buffer.Count - copylen);
                                        buffer = new ArraySegment<byte>(buffer.Array, 0, buffer.Count - copylen);
                                        c._recvbuffers.Set(buffer);
                                    }
                                    c._len -= copylen;
                                    if (c._recvbuffers.Count == 0) break;
                                }
                                if (c._len <= 0)
                                {
                                    c._len = 0;
                                    data = (byte[])c._recvdata.Clone();
                                    f d = delegate()
                                    {
                                        handler.OnRecv((ushort)c._id, data);
                                    };
                                    op.Enqueue(d);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                Debug.WriteLine(ex.StackTrace);
                                f d = delegate()
                                {
                                    Close((ushort)c._id);
                                };
                                op.Enqueue(d);
                            }
                        }
                    }
                    Thread.CurrentThread.Join(1);
                }
            });

            t.IsBackground = true;
            u.IsBackground = true;

            running = true;

            t.Start();
            u.Start();

            server.Start(nCount);
        }

        public void Finish()
        {
            try
            {
                running = false;
                server.Stop();

                if (peers.Count > 0)
                {
                    Peer[] clients;
                    locker.AcquireReaderLock(Timeout.Infinite);
                    clients = new Peer[peers.Count];
                    peers.Values.CopyTo(clients, 0);
                    locker.ReleaseReaderLock();

                    for (int i = 0; i < clients.Length; ++i)
                    {
                        Close((ushort)clients[i]._id);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        public IPEndPoint GetRemoteAddress(int nID)
        {
            if (GetPeer(nID) == null) return null;
            return (IPEndPoint)GetPeer(nID).client.Client.RemoteEndPoint;
        }

        public void GetRemoteAddress(int nID, ref int host, ref int nPort)
        {
            IPEndPoint ep = GetRemoteAddress(nID);
            if (ep == null) return;
            host = BitConverter.ToInt32(ep.Address.GetAddressBytes(), 0);
            nPort = ep.Port;
        }

        public int GetSendBufferCount(int nID)
        {
            return sendbuffers.Count;
        }

        public void Send(int nID, byte[] data)
        {
            Peer c = GetPeer(nID);
            if (c == null) return;

            byte[] len = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
            byte[] buff = new byte[len.Length + data.Length];
            Buffer.BlockCopy(len, 0, buff, 0, len.Length);
            Buffer.BlockCopy(data, 0, buff, len.Length, data.Length);

            SendBuff s = new SendBuff();
            s.peer = c;
            s.buff = buff;
            sendbuffers.Enqueue(s);
        }

        long GetTotalMemory()
        {
            long bytes = 0;
            if (peers.Count > 0)
            {
                Peer[] clients;
                locker.AcquireReaderLock(Timeout.Infinite);
                clients = new Peer[peers.Count];
                peers.Values.CopyTo(clients, 0);
                locker.ReleaseReaderLock();

                foreach (Peer peer in clients)
                {
                    bytes += peer.MemoryUsage;
                }
            }

            return bytes + sendbuffers.TotalBytes;
        }

        public void Pump()
        {
            while (server.Pending())
            {
                try
                {
                    long mem = GetTotalMemory();
                    if (mem > maxMemoryUsage) break;

                    if (peers.Count >= count) break;
                    server.BeginAcceptTcpClient(delegate(IAsyncResult ar)
                    {
                        try
                        {
                            long memtest = GetTotalMemory();
                            if (memtest > maxMemoryUsage)
                            {
                                server.EndAcceptTcpClient(ar).Client.Close();
                                return;
                            }

                            Peer c = new Peer(server.EndAcceptTcpClient(ar));
                            c.client.LingerState = new LingerOption(false, 0);
                            c.client.NoDelay = true;
                            c.client.Client.LingerState = new LingerOption(false, 0);
                            c.client.Client.NoDelay = true;
                            c.client.Client.Blocking = false;
                            c.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                            locker.AcquireWriterLock(Timeout.Infinite);
                            peers[c._id] = c;
                            locker.ReleaseWriterLock();

                            f d = delegate()
                            {
                                handler.OnAccept((ushort)c._id);
                            };
                            op.Enqueue(d);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                            Debug.WriteLine(ex.StackTrace);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }
            }

            if (peers.Count > 0)
            {
                Peer[] clients;
                locker.AcquireReaderLock(Timeout.Infinite);
                clients = new Peer[peers.Count];
                peers.Values.CopyTo(clients, 0);
                locker.ReleaseReaderLock();

                DateTime now = DateTime.Now;

                for (int i = 0; i < clients.Length; ++i)
                {
                    Peer peer = clients[i];
                    bool closed = false;

                    do
                    {
                        if (!peer.Connected)
                        {
                            closed = true;
                            break;
                        }

                        if (now - peer._lastdisconnectcheck >= disconnectCheckSpan)
                        {
                            if (peer.client.Client.Poll(0, SelectMode.SelectWrite))
                            {
                                byte[] buff = new byte[0];
                                try
                                {
                                    peer.client.Client.Send(buff);
                                }
                                catch (SocketException se)
                                {
                                    if (se.SocketErrorCode == SocketError.ConnectionReset)
                                    {
                                    }
                                    else if (se.SocketErrorCode == SocketError.ConnectionAborted)
                                    {
                                    }
                                    else
                                    {
                                        Debug.WriteLine(se.SocketErrorCode.ToString());
                                        Debug.WriteLine(se.ErrorCode);
                                    }
                                    closed = true;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                    Debug.WriteLine(ex.StackTrace);
                                    closed = true;
                                    break;
                                }
                            }
                        }

                        if (peer.client.Client.Poll(0, SelectMode.SelectError))
                        {
                            Debug.WriteLine("ERROR CLOSE");
                            closed = true;
                            break;
                        }
                    } while (false);

                    if (closed)
                    {
                        Close((ushort)peer._id);
                    }
                }
            }

            while (sendbuffers.Count > 0)
            {
                SendBuff s = sendbuffers.Dequeue();
                if (!s.peer.Connected) continue;
                if (s.peer.client.Client.Poll(0, SelectMode.SelectWrite))
                {
                    try
                    {
                        s.peer.client.Client.BeginSend(s.buff, 0, s.buff.Length, SocketFlags.None, delegate(IAsyncResult ar)
                        {
                            try
                            {
                                s.peer.client.Client.EndSend(ar);
                            }
                            catch (ObjectDisposedException) { }
                            catch (SocketException)
                            {
                                Close((ushort)s.peer._id);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                Debug.WriteLine(ex.StackTrace);
                                Close((ushort)s.peer._id);
                            }
                        }, null);
                    }
                    catch (SocketException)
                    {
                        Close((ushort)s.peer._id);
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                        Close((ushort)s.peer._id);
                    }
                }
            }

            while (op.Count > 0)
            {
                (op.Dequeue() as f)();
            }
        }
    }

    public interface INetEvent
    {
        void OnAccept(int id);
        void OnClose(int id);
        void OnConnect(int id);
        void OnConnectFailed();
        void OnConnect(int id, object state);
        void OnConnectFailed(object state);
        void OnRecv(int id, byte[] data);
    }
}
