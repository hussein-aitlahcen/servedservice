using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using CodeProject.ObjectPool;

namespace ServedService
{
    internal sealed class BigBuffer : IDisposable
    {
        public byte[] Buffer { get; private set; }
        private ConcurrentStack<BufferSegment> _segments; 

        public BigBuffer(int segmentSize, int segmentCount)
        {
            _segments = new ConcurrentStack<BufferSegment>();
            Buffer = new byte[segmentSize * segmentCount];
            for (int i = 0; i < Buffer.Length; i += segmentSize)
            {
                _segments.Push(new BufferSegment(i, segmentSize));
            }
        }

        public BufferSegment Acquire()
        {
            BufferSegment segment;
            _segments.TryPop(out segment);
            return segment;
        }

        public void Release(BufferSegment segment)
        {
            _segments.Push(segment);
        }

        public void Dispose()
        {
            _segments.Clear();
            _segments = null;

            Buffer = null;
        }
    }

    internal sealed class BufferSegment
    {
        public int Offset { get; private set; }
        public int Count { get; private set; }

        public BufferSegment(int offset, int count)
        {
            Offset = offset;
            Count = count;
        }
    }

    internal sealed class WrappedSocketEvent : SocketAsyncEventArgs
    {
        public BufferSegment Segment { get; set; }
    }

    internal sealed class ServerHost
    {
        private const int BufferSegmentSize = 1024;
        private const int MaxBufferSegment = 1000;
        private const int DefaultBacklog = 100;

        internal delegate void BytesReceived(Socket socket, Stream stream);

        private BigBuffer _bigBuffer;
        private Socket _socket;
        private string _host;
        private int _port;
        private readonly int _backlog;
        private readonly ObjectPool<PooledObjectWrapper<SocketAsyncEventArgs>> _sendPool;

        public event BytesReceived OnBytesReceived;

        internal ServerHost(string host, int port, int backlog = DefaultBacklog)
        {
            _backlog = backlog;
            _host = host;
            _port = port;
            _sendPool = new ObjectPool<PooledObjectWrapper<SocketAsyncEventArgs>>(() =>
            {
                var saea = new SocketAsyncEventArgs();
                saea.Completed += IOCompleted;
                var pooled = new PooledObjectWrapper<SocketAsyncEventArgs>(saea);
                saea.UserToken = pooled;
                return pooled;
            });
        }
        
        internal void Start()
        {
            _bigBuffer = new BigBuffer(BufferSegmentSize, MaxBufferSegment);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                Blocking = false
            };
            _socket.Bind(new IPEndPoint(IPAddress.Parse(_host), _port));
            _socket.Listen(_backlog);
            for (var i = 0; i < _backlog; i++)
            {
                StartAccept();
            }
        }

        internal void Stop()
        {
            _bigBuffer.Dispose();
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                _socket.Dispose();
            }
            catch
            {
            }
        }

        private void StartAccept(SocketAsyncEventArgs args = null)
        {
            if (args == null)
            {
                args = new SocketAsyncEventArgs();
                args.Completed += IOCompleted;
            }
            else
            {
                args.AcceptSocket = null;
            }

            if (!_socket.AcceptAsync(args))
                ProcessAccepted(args);
        }

        private void StartReceive(Socket socket, WrappedSocketEvent args = null)
        {
            if (args == null)
            {
                args = CreateEventArgs();
                args.AcceptSocket = socket;
            }
            if (!socket.ReceiveAsync(args))
                ProcessReceived(args);
        }

        private WrappedSocketEvent CreateEventArgs()
        {
            var saea = new WrappedSocketEvent()
            {
                Segment = _bigBuffer.Acquire()
            };
            saea.SetBuffer(_bigBuffer.Buffer, saea.Segment.Offset, saea.Segment.Count);
            saea.Completed += IOCompleted;
            return saea;
        }

        private void DestroyEventArgs(SocketAsyncEventArgs saea)
        {
            _bigBuffer.Release(((WrappedSocketEvent)saea).Segment);
            saea.Completed -= IOCompleted;
            saea.SetBuffer(null, 0, 0);
            saea.AcceptSocket.Close();
            saea.AcceptSocket.Dispose();
            saea.Dispose();
        }

        private void IOCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccepted(args);
                    break;

                case SocketAsyncOperation.Receive:
                    ProcessReceived((WrappedSocketEvent)args);
                    break;

                case SocketAsyncOperation.Send:
                    ProcessSent(args);
                    break;

                default:
                    Console.WriteLine("unknow operation : " + args.LastOperation);
                    break;
            }   
        }

        private void ProcessAccepted(SocketAsyncEventArgs args)
        {
            var accepted = args.AcceptSocket;
            StartAccept(args);
            StartReceive(accepted);
        }

        private void ProcessReceived(WrappedSocketEvent args)
        {
            if (args.BytesTransferred == 0)
            {
                DestroyEventArgs(args);
                return;
            }

            if(OnBytesReceived != null)
                OnBytesReceived(args.AcceptSocket, new MemoryStream(args.Buffer, args.Segment.Offset, args.BytesTransferred));

            StartReceive(args.AcceptSocket, args);
        }

        private void ProcessSent(SocketAsyncEventArgs args)
        {
            args.SetBuffer(null, 0, 0);
            ((PooledObjectWrapper<SocketAsyncEventArgs>) args.UserToken).Dispose();
        }

        internal void Send(Socket socket, byte[] data)
        {
            var saea = _sendPool.GetObject().InternalResource;
            saea.SetBuffer(data, 0, data.Length);
            socket.SendAsync(saea);
        }
    }
}
