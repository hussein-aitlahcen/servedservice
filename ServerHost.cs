using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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

        public event BytesReceived OnBytesReceived;

        internal ServerHost(string host, int port, int backlog = DefaultBacklog)
        {
            _backlog = backlog;
            _host = host;
            _port = port;
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
            for (int i = 0; i < _backlog; i++)
            {
                AcceptAsync();
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

        private void AcceptAsync()
        {
            _socket.AcceptAsync(CreateEventArgs());
        }

        private WrappedSocketEvent CreateEventArgs()
        {
            var saea = new WrappedSocketEvent()
            {
                Segment = _bigBuffer.Acquire()
            };
            saea.Completed += IOCompleted;
            return saea;
        }

        private void DestroyEventArgs(SocketAsyncEventArgs saea)
        {
            saea.Completed -= IOCompleted;
            _bigBuffer.Release(((WrappedSocketEvent)saea).Segment);
        }

        private void IOCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccepted((WrappedSocketEvent)args);
                    break;

                case SocketAsyncOperation.Receive:
                    ProcessReceived((WrappedSocketEvent)args);
                    break;

                case SocketAsyncOperation.Send:
                    ProcessSent(args);
                    break;
            }   
        }

        private void ProcessAccepted(WrappedSocketEvent args)
        {
            AcceptAsync();
            args.SetBuffer(_bigBuffer.Buffer, args.Segment.Offset, args.Segment.Count);
            args.AcceptSocket.ReceiveAsync(args);
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

            args.AcceptSocket.ReceiveAsync(args);
        }

        private void ProcessSent(SocketAsyncEventArgs args)
        {
            args.Completed -= IOCompleted;
            args.SetBuffer(null, 0, 0);
        }

        internal void Send(Socket socket, byte[] data)
        {
            var saea = new SocketAsyncEventArgs();
            saea.SetBuffer(data, 0, data.Length);
            saea.Completed += IOCompleted;
            socket.SendAsync(saea);
        }
    }
}
