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


        public void Init(IPEndPoint iPEndPoint)
        {
            _listenersocket = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _listenersocket.Bind(iPEndPoint);

            _listenersocket.Listen(10);
        }


        public Socket Accept()
        {

            /* Accept와 같은 직접 연결에 관여하는 함수를 사용하면 (Blocking)
            온라인게임 같이 유동적으로 접속을 처리하고 많은 유저들이 동시 다발적으로 불규칙적이게 접속을 하게 될때
            문제가 생김, 그래서 asnyc(비동기)로 서버의 접속을 처리하여. callback함수를 만들어주는것이 좋음

            return _listenersocket.Accept();
            */

            //윗 코드와 다른 점 : return 전에 Asnyc함수를 호출함

            _listenersocket.AcceptAsync();
            return _listenersocket.Accept();






        }
    }
}