using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
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
}
