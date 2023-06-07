using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Listener
    {
        Socket _listenersocket;

        Action<Socket> _onAcceptHandler;

        public void Init(IPEndPoint iPEndPoint , Action<Socket> onAcceptHandler)
        {

            _onAcceptHandler = null;
            // 혹시 모르는 상황에 대한 모르는 초기화작업
            _onAcceptHandler += onAcceptHandler;

            _listenersocket = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _listenersocket.Bind(iPEndPoint);

            _listenersocket.Listen(10);

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            args.Completed += OnAcceptCompleted;


            //최초 접속자 검출 실행
            RegisterAccept(args);

        }

       

        public void RegisterAccept(SocketAsyncEventArgs eventArgs)
        {
            eventArgs.AcceptSocket = null;           
            //[AcceptAsync]asnyc에서 돌아가는 함수들은 별도의 스레드에서 돌아간다.

            bool isPending = _listenersocket.AcceptAsync(eventArgs);
            if (isPending == false)
            {
                OnAcceptCompleted(null, eventArgs);
            }
            //Accept();
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if(e.SocketError == SocketError.Success)
            {
                _onAcceptHandler.Invoke(e.AcceptSocket);

            }
            else
            {
                Console.WriteLine($"OnAcceptCompleted ] Error!");

            }

            RegisterAccept(e);
        }



        public Socket Accept()
        {

            /* Accept와 같은 직접 연결에 관여하는 함수를 사용하면 (Blocking)
            온라인게임 같이 유동적으로 접속을 처리하고 많은 유저들이 동시 다발적으로 불규칙적이게 접속을 하게 될때
            문제가 생김, 그래서 asnyc(비동기)로 서버의 접속을 처리하여. callback함수를 만들어주는것이 좋음

            return _listenersocket.Accept();
            */

            //return 전에 Asnyc(비동기)함수를 호출함

            // Idea -> Register

            return _listenersocket.Accept();
            



        }
    }
}