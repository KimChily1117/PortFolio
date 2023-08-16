using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
<<<<<<< Updated upstream
    public abstract class Session
    {
        Socket _socket;
        volatile int isDisconnect = 0;


        private bool _isPending = false;

        private Queue<byte[]> _sendBuffer = new Queue<byte[]>();
        private object _lock = new object();


        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        #region CallbackMethod(개발자가 임의로 만든 CallbackMethod)

        public abstract void OnConected(EndPoint param);
        public abstract int  OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);
        

        #endregion CallbackMethod(개발자가 임의로 만든 CallbackMethod)
        
        
        
        public void Start(Socket socket)
        {
            _socket = socket;



            byte[] recvBuff = new byte[1024];

            _recvArgs.SetBuffer(recvBuff, 0, 1024);
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);


            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendComplete);
            RegisterRecv(_recvArgs);
        }

        #region Network(Recv)
        public void RegisterRecv(SocketAsyncEventArgs eventArgs)
        {
            eventArgs.AcceptSocket = null;

            bool isPending = _socket.ReceiveAsync(eventArgs);

            if (isPending == false)
            {
                OnRecvCompleted(null, eventArgs);
            }
        }


        public void OnRecvCompleted(object sender, SocketAsyncEventArgs evtArgs)
        {
            if (evtArgs.BytesTransferred > 0 && evtArgs.SocketError == SocketError.Success)
            {
                try
                {
                    string recvData = Encoding.UTF8.GetString(evtArgs.Buffer,
                        evtArgs.Offset,
                        evtArgs.BytesTransferred);
                    Console.WriteLine($"Client -> Server : {recvData}");
                }
                catch (Exception)
                {

                    throw;
                }

            }
            else
            {
                Disconnect();
            }
        }
        #endregion Network(Recv)


        #region Network(Send)
        public void Send(byte[] recvBuff)
        {

            lock (_lock)
            {
                _sendBuffer.Enqueue(recvBuff);
                if (_isPending == false)
                    RegisterSend();
            }

        }

        public void RegisterSend()
        {

            _isPending = true;
            
            ///////////////////////////////////////////////////
            //byte[] buff = _sendBuffer.Dequeue();
            //_sendArgs.SetBuffer(buff, 0, buff.Length);
            //bool isPending = _socket.SendAsync(_sendArgs);
            // 위에 code같은경우 Async함수에 단일 Buffer를 보내준다.
            // 이 방법이 나븐건 아님(Lock을 걸어서. 하나의 작업을 완료 할때 까지 스레드를 제한 하기 때문)


            while (_sendBuffer.Count > 0)
            {
                byte[] buff = _sendBuffer.Dequeue();
                _sendArgs.BufferList.Add(new ArraySegment<byte>(buff, 0, buff.Length));// 이렇게 Set 하면 안됨.
            }

            bool isPending = _socket.SendAsync(_sendArgs);

            if (isPending == false)
            {
                OnSendComplete(null, _sendArgs);
            }

            else
            {

            }
        }


        public void OnSendComplete(object sender, SocketAsyncEventArgs args)
        {

            lock (_lock)
            {
                if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)

                {
                    try
                    {
                        if(_sendBuffer.Count > 0 )
                        {
                            RegisterSend();
                        }
                        else
                        {
                            _isPending = false;
                        }

                    }
                    catch (Exception ex)
                    {

                    }
                }

                else
                {
                    Console.WriteLine($"OnSendComplete] Error!!");
                }
            }
        }

        #endregion Network(Send)




        public void Disconnect()
        {
            // 예외처리 추가 Socket통신은 별도의 스레드를 사용하여 연결 하기 때문에. Interlocked를 사용하여 동시에 발생하는 이벤트를
            // 방지 할 수 있다 
            if (Interlocked.Exchange(ref isDisconnect, 1) == 1)
            {
                return;
            }

            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

    }
=======
	public abstract class PacketSession : Session
	{
		public static readonly int HeaderSize = 2;

		// [size(2)][packetId(2)][ ... ][size(2)][packetId(2)][ ... ]
		public sealed override int OnRecv(ArraySegment<byte> buffer)
		{
			int processLen = 0;

			while (true)
			{
				// 최소한 헤더는 파싱할 수 있는지 확인
				if (buffer.Count < HeaderSize)
					break;

				// 패킷이 완전체로 도착했는지 확인
				ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
				if (buffer.Count < dataSize)
					break;

				// 여기까지 왔으면 패킷 조립 가능
				OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
				
				processLen += dataSize;
				buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
			}

			return processLen;
		}

		public abstract void OnRecvPacket(ArraySegment<byte> buffer);
	}

	public abstract class Session
	{
		Socket _socket;
		int _disconnected = 0;

		RecvBuffer _recvBuffer = new RecvBuffer(1024);

		object _lock = new object();
		Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
		List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
		SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
		SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

		public abstract void OnConnected(EndPoint endPoint);
		public abstract int  OnRecv(ArraySegment<byte> buffer);
		public abstract void OnSend(int numOfBytes);
		public abstract void OnDisconnected(EndPoint endPoint);

		public void Start(Socket socket)
		{
			_socket = socket;

			_recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
			_sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

			RegisterRecv();
		}

		public void Send(ArraySegment<byte> sendBuff)
		{
			lock (_lock)
			{
				_sendQueue.Enqueue(sendBuff);
				if (_pendingList.Count == 0)
					RegisterSend();
			}
		}

		public void Disconnect()
		{
			if (Interlocked.Exchange(ref _disconnected, 1) == 1)
				return;

			OnDisconnected(_socket.RemoteEndPoint);
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
		}

		#region 네트워크 통신

		void RegisterSend()
		{
			while (_sendQueue.Count > 0)
			{
				ArraySegment<byte> buff = _sendQueue.Dequeue();
				_pendingList.Add(buff);
			}
			_sendArgs.BufferList = _pendingList;

			bool pending = _socket.SendAsync(_sendArgs);
			if (pending == false)
				OnSendCompleted(null, _sendArgs);
		}

		void OnSendCompleted(object sender, SocketAsyncEventArgs args)
		{
			lock (_lock)
			{
				if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
				{
					try
					{
						_sendArgs.BufferList = null;
						_pendingList.Clear();

						OnSend(_sendArgs.BytesTransferred);

						if (_sendQueue.Count > 0)
							RegisterSend();
					}
					catch (Exception e)
					{
						Console.WriteLine($"OnSendCompleted Failed {e}");
					}
				}
				else
				{
					Disconnect();
				}
			}
		}

		void RegisterRecv()
		{
			_recvBuffer.Clean();
			ArraySegment<byte> segment = _recvBuffer.WriteSegment;
			_recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

			bool pending = _socket.ReceiveAsync(_recvArgs);
			if (pending == false)
				OnRecvCompleted(null, _recvArgs);
		}

		void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
		{
			if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
			{
				try
				{
					// Write 커서 이동
					if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
					{
						Disconnect();
						return;
					}

					// 컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리했는지 받는다
					int processLen = OnRecv(_recvBuffer.ReadSegment);
					if (processLen < 0 || _recvBuffer.DataSize < processLen)
					{
						Disconnect();
						return;
					}

					// Read 커서 이동
					if (_recvBuffer.OnRead(processLen) == false)
					{
						Disconnect();
						return;
					}

					RegisterRecv();
				}
				catch (Exception e)
				{
					Console.WriteLine($"OnRecvCompleted Failed {e}");
				}
			}
			else
			{
				Disconnect();
			}
		}

		#endregion
	}
>>>>>>> Stashed changes
}
