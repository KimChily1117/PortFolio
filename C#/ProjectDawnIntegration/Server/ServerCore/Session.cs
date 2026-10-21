using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;

        // [size(2)][packetId(2)][ ... ][size(2)][packetId(2)][ ... ]
        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            int processLen = 0;
            int packetCount = 0;

            while (true)
            {
                // 최소한 헤더는 파싱할 수 있는지 확인
                if (buffer.Count < HeaderSize)
                    break;

                // 패킷이 완전체로 도착했는지 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (dataSize < HeaderSize + sizeof(ushort))
                    throw new FormatException("Packet size must include both size and message id.");
                if (buffer.Count < dataSize)
                    break;

                // 여기까지 왔으면 패킷 조립 가능
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                packetCount++;

                processLen += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }



            return processLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }

    // One bounded queue owns both queued and in-flight bytes. A short TCP send consumes only
    // BytesTransferred; the remaining suffix is retained for the next SendAsync call.
    public sealed class BoundedSendQueue
    {
        readonly LinkedList<ArraySegment<byte>> buffers = new LinkedList<ArraySegment<byte>>();
        readonly int limit;
        public int Bytes { get; private set; }
        public BoundedSendQueue(int limit = 4 * 1024 * 1024) { if(limit<1)throw new ArgumentOutOfRangeException(nameof(limit));this.limit=limit; }
        public bool TryEnqueue(IReadOnlyList<ArraySegment<byte>> incoming)
        {
            long total=Bytes;
            foreach(var b in incoming) {if(b.Array==null)throw new ArgumentException("Missing send buffer");total+=b.Count;}
            if(total>limit)return false;
            foreach(var b in incoming)if(b.Count>0)buffers.AddLast(b);
            Bytes=(int)total;return true;
        }
        public List<ArraySegment<byte>> Batch(int maxBytes=256*1024, int maxSegments=64)
        {
            var result=new List<ArraySegment<byte>>();int left=maxBytes;
            foreach(var b in buffers) {
                if(left<=0||result.Count==maxSegments)break;
                int count=Math.Min(b.Count,left);result.Add(new ArraySegment<byte>(b.Array,b.Offset,count));left-=count;
            }
            return result;
        }
        public void Consume(int count)
        {
            if(count<1||count>Bytes)throw new ArgumentOutOfRangeException(nameof(count));
            Bytes-=count;
            while(count>0) {
                var b=buffers.First.Value;
                if(count>=b.Count){count-=b.Count;buffers.RemoveFirst();}
                else {buffers.First.Value=new ArraySegment<byte>(b.Array,b.Offset+count,b.Count-count);count=0;}
            }
        }
        public void Clear(){buffers.Clear();Bytes=0;}
    }

    public abstract class Session
    {
        Socket _socket;
        int _disconnected;
        readonly RecvBuffer _recvBuffer = new RecvBuffer(65535);
        readonly object _lock = new object();
        readonly BoundedSendQueue _sendQueue = new BoundedSendQueue();
        readonly SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        readonly SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        bool _sending;
        int _batchBytes;
        public int PendingSendBytes {get {lock(_lock)return _sendQueue.Bytes;}}
        public abstract void OnConnected(EndPoint endPoint);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);
        public void Start(Socket socket)
        {
            if(_socket!=null)throw new InvalidOperationException("Session already started");
            _socket=socket;
            _recvArgs.Completed+=OnRecvCompleted;
            _sendArgs.Completed+=OnSendCompleted;
            RegisterRecv();
        }
        public void Send(ArraySegment<byte> buffer)=>Send(new List<ArraySegment<byte>>{buffer});
        public void Send(List<ArraySegment<byte>> buffers)
        {
            lock(_lock) {
                if(_disconnected!=0)return;
                if(!_sendQueue.TryEnqueue(buffers)){Disconnect();return;}
                if(!_sending && _sendQueue.Bytes>0) {_sending=true;PumpSend();}
            }
        }
        public void Disconnect()
        {
            if(Interlocked.Exchange(ref _disconnected,1)!=0)return;
            EndPoint peer=null;try{peer=_socket?.RemoteEndPoint;}catch(SocketException){}catch(ObjectDisposedException){}
            try{OnDisconnected(peer);}catch(Exception e){Console.WriteLine("Disconnect callback: "+e.GetType().Name);}
            finally {
                try{_socket?.Shutdown(SocketShutdown.Both);}catch(SocketException){}catch(ObjectDisposedException){}
                _socket?.Close();lock(_lock){_sendQueue.Clear();}
            }
        }
        // Called with _lock held. Synchronous completions use a loop, not recursive callbacks.
        void PumpSend()
        {
            try {
                while(_disconnected==0 && _sendQueue.Bytes>0) {
                    var batch=_sendQueue.Batch();_batchBytes=0;foreach(var b in batch)_batchBytes+=b.Count;
                    _sendArgs.BufferList=batch;
                    if(_socket.SendAsync(_sendArgs))return;
                    if(!CompleteSend(_sendArgs))return;
                }
                _sending=false;
            }catch(Exception e){Console.WriteLine("Send failed: "+e.GetType().Name);Disconnect();}
        }
        bool CompleteSend(SocketAsyncEventArgs args)
        {
            _sendArgs.BufferList=null;
            if(_disconnected!=0)return false;
            if(args.SocketError!=SocketError.Success||args.BytesTransferred<=0||args.BytesTransferred>_batchBytes){Disconnect();return false;}
            _sendQueue.Consume(args.BytesTransferred);OnSend(args.BytesTransferred);return true;
        }
        void OnSendCompleted(object sender,SocketAsyncEventArgs args)
        {
            lock(_lock)try{if(CompleteSend(args))PumpSend();}catch(Exception e){Console.WriteLine("Send completion: "+e.GetType().Name);Disconnect();}
        }
        void RegisterRecv()
        {
            try {
                while(_disconnected==0) {
                    _recvBuffer.Clean();var segment=_recvBuffer.WriteSegment;
                    if(segment.Count==0){Disconnect();return;}
                    _recvArgs.SetBuffer(segment.Array,segment.Offset,segment.Count);
                    if(_socket.ReceiveAsync(_recvArgs))return;
                    if(!CompleteRecv(_recvArgs))return;
                }
            }catch(Exception e){Console.WriteLine("Receive failed: "+e.GetType().Name);Disconnect();}
        }
        bool CompleteRecv(SocketAsyncEventArgs args)
        {
            if(_disconnected!=0)return false;
            if(args.SocketError!=SocketError.Success||args.BytesTransferred<=0||!_recvBuffer.OnWrite(args.BytesTransferred)){Disconnect();return false;}
            int processed=OnRecv(_recvBuffer.ReadSegment);
            if(processed<0||processed>_recvBuffer.DataSize||!_recvBuffer.OnRead(processed)){Disconnect();return false;}
            return _disconnected==0;
        }
        void OnRecvCompleted(object sender,SocketAsyncEventArgs args)
        {
            try{if(CompleteRecv(args))RegisterRecv();}catch(Exception e){Console.WriteLine("Receive completion: "+e.GetType().Name);Disconnect();}
        }
    }
}
